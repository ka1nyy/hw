using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;


static class SubscriptionAuth
{
    const string Issuer = "https://auth.openai.com";
    const string Resource = "https://api.openai.com/v1";
    const string Scope = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Jarvis.Subscription.OpenAI.v1");

    internal static string DirectoryPath
    {
        get
        {
#if FRIEND
            return Program.BaseDir;
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Jarvis", "Subscriptions", "openai");
#endif
        }
    }
    internal static string CredentialPath
    {
        get
        {
#if FRIEND
            return Path.Combine(DirectoryPath, ".edge-subscription.dpapi");
#else
            return Path.Combine(DirectoryPath, "connection.dpapi");
#endif
        }
    }
    static string HostPath
    {
        get
        {
#if FRIEND
            return Path.Combine(DirectoryPath, ".edge-subscription-host");
#else
            return Path.Combine(DirectoryPath, "host.json");
#endif
        }
    }
    internal static bool IsConnected { get { return File.Exists(CredentialPath); } }
    static JavaScriptSerializer Json()
    {
        JavaScriptSerializer json = new JavaScriptSerializer();
        json.MaxJsonLength = 4 * 1024 * 1024;
        return json;
    }
    internal static string Text(Dictionary<string, object> value, string key)
    {
        object item;
        return value != null && value.TryGetValue(key, out item) ? item as string : null;
    }
    static Dictionary<string, object> Read()
    {
        byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(CredentialPath), Entropy,
            DataProtectionScope.CurrentUser);
        try { return Json().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(plain)); }
        finally { Array.Clear(plain, 0, plain.Length); }
    }
    static void Save(Dictionary<string, object> value, bool replace)
    {
        Directory.CreateDirectory(DirectoryPath);
        string temp = Path.Combine(DirectoryPath, ".auth-" + Guid.NewGuid().ToString("N") + ".tmp");
        byte[] plain = Encoding.UTF8.GetBytes(Json().Serialize(value));
        try
        {
            File.WriteAllBytes(temp, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
            if (replace && File.Exists(CredentialPath)) File.Replace(temp, CredentialPath, null);
            else File.Move(temp, CredentialPath);
        }
        finally
        {
            Array.Clear(plain, 0, plain.Length);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
    static Mutex Lock()
    {
        string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
        Mutex gate = new Mutex(false, @"Local\JarvisSubscription_" + sid);
        bool acquired;
        try { acquired = gate.WaitOne(30000); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) { gate.Dispose(); throw new Exception("Подключение аккаунта занято. Повторите запрос."); }
        return gate;
    }
    static Dictionary<string, object> RequestJson(string url, Dictionary<string, string> form)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.AllowAutoRedirect = false;
        request.Timeout = 25000;
        request.ReadWriteTimeout = 25000;
        if (form != null)
        {
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            byte[] bytes = Encoding.UTF8.GetBytes(Encode(form));
            request.ContentLength = bytes.Length;
            using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
        }
        try
        {
            using (WebResponse response = request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return Json().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
        }
        catch (WebException error)
        {
            HttpWebResponse response = error.Response as HttpWebResponse;
            int status = response == null ? 0 : (int)response.StatusCode;
            if (response != null) response.Close();
            if (status == 400 || status == 401)
                throw new Exception("Вход в ChatGPT истёк. Подключите подписку заново.");
            throw new Exception("Не удалось обновить вход ChatGPT" + (status == 0 ? "." : " (HTTP " + status + ")."));
        }
    }
    static string Encode(Dictionary<string, string> values)
    {
        List<string> items = new List<string>();
        foreach (KeyValuePair<string, string> value in values)
            items.Add(Uri.EscapeDataString(value.Key) + "=" + Uri.EscapeDataString(value.Value));
        return string.Join("&", items.ToArray());
    }
    internal static string AccessToken()
    {
        using (Mutex gate = Lock())
        {
            try
            {
                Dictionary<string, object> saved = Read();
                CheckScopes(saved);
                DateTime savedAt = DateTime.Parse(Text(saved, "saved_at"), null,
                    System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
                if (savedAt.AddSeconds(Convert.ToDouble(saved["expires_in"]) - 300) > DateTime.UtcNow)
                    return Text(saved, "access_token");
                string clientId = Text(saved, "client_id");
                string refresh = Text(saved, "refresh_token");
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(refresh))
                    throw new Exception("Подключите подписку ChatGPT заново.");


                Dictionary<string, object> publicKeys = RequestJson(Issuer + "/.well-known/jwks.json", null);
                Dictionary<string, object> tokens = RequestJson(Issuer + "/api/accounts/oauth/token",
                    new Dictionary<string, string> { { "grant_type", "refresh_token" },
                        { "client_id", clientId }, { "refresh_token", refresh }, { "resource", Resource } });
                string idToken = Text(tokens, "id_token");
                if (!string.IsNullOrEmpty(idToken))
                {
                    Dictionary<string, object> identity = ValidateIdentity(idToken, clientId, null, publicKeys);
                    if (Text(identity, "sub") != Text(saved, "subject"))
                        throw new Exception("Обновление входа вернуло другой аккаунт.");
                    saved["id_token"] = idToken;
                }
                string access = Text(tokens, "access_token");
                string rotated = Text(tokens, "refresh_token");
                if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(rotated) ||
                    !tokens.ContainsKey("expires_in") || Convert.ToDouble(tokens["expires_in"]) <= 0)
                    throw new Exception("Сервер вернул неполное обновление входа.");
                saved["access_token"] = access;
                saved["refresh_token"] = rotated;
                saved["expires_in"] = tokens["expires_in"];
                if (tokens.ContainsKey("scope")) saved["scopes"] = Text(tokens, "scope").Split(' ');
                saved["saved_at"] = DateTime.UtcNow.ToString("o");
                CheckScopes(saved);
                Save(saved, true);
                return access;
            }
            finally { gate.ReleaseMutex(); }
        }
    }
    static void CheckScopes(Dictionary<string, object> saved)
    {
        object scopes;
        bool direct = false, invoke = false;
        if (saved.TryGetValue("scopes", out scopes) && scopes is IEnumerable)
            foreach (object scope in (IEnumerable)scopes)
            {
                if (scope as string == "chatgpt.tokens.use.direct") direct = true;
                if (scope as string == "resource.invoke") invoke = true;
            }
        if (!direct || !invoke) throw new Exception("Не разрешено использование подписки ChatGPT.");
    }
    static byte[] Decode(string text)
    {
        string padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
    }
    static string RandomToken()
    {
        byte[] bytes = new byte[32];
        using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    internal static Dictionary<string, object> ValidateIdentity(string jwt, string clientId, string nonce)
    {
        return ValidateIdentity(jwt, clientId, nonce, RequestJson(Issuer + "/.well-known/jwks.json", null));
    }
    static Dictionary<string, object> ValidateIdentity(string jwt, string clientId, string nonce, Dictionary<string, object> keys)
    {
        string[] parts = jwt.Split('.');
        if (parts.Length != 3) throw new Exception("Некорректное подтверждение входа.");
        Dictionary<string, object> header = Json().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(Decode(parts[0])));
        if (Text(header, "alg") != "RS256") throw new Exception("Неподдерживаемая подпись входа.");
        bool verified = false;
        foreach (Dictionary<string, object> key in (IEnumerable)keys["keys"])
        {
            if (Text(key, "kid") != Text(header, "kid") || Text(key, "kty") != "RSA") continue;
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.ImportParameters(new RSAParameters { Modulus = Decode(Text(key, "n")), Exponent = Decode(Text(key, "e")) });
                verified = rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]),
                    CryptoConfig.MapNameToOID("SHA256"), Decode(parts[2]));
            }
            if (verified) break;
        }
        if (!verified) throw new Exception("Не удалось проверить подпись аккаунта.");
        Dictionary<string, object> identity = Json().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(Decode(parts[1])));
        double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        bool audience = Text(identity, "aud") == clientId;
        object audiences;
        int audienceCount = 0;
        if (identity.TryGetValue("aud", out audiences) && audiences is IEnumerable && !(audiences is string))
            foreach (object item in (IEnumerable)audiences) { audienceCount++; if (item as string == clientId) audience = true; }
        if (Text(identity, "iss") != Issuer || !audience ||
            !identity.ContainsKey("exp") || Convert.ToDouble(identity["exp"]) <= now ||
            (identity.ContainsKey("nbf") && Convert.ToDouble(identity["nbf"]) > now + 60) ||
            (identity.ContainsKey("azp") && Text(identity, "azp") != clientId) ||
            (audienceCount > 1 && Text(identity, "azp") != clientId) ||
            (nonce != null && Text(identity, "nonce") != nonce) || string.IsNullOrEmpty(Text(identity, "sub")))
            throw new Exception("Подтверждение аккаунта не соответствует запросу входа.");
        return identity;
    }
    internal static string Connect()
    {
        using (Mutex gate = Lock())
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                Dictionary<string, object> old = IsConnected ? Read() : null;
                string host;
                if (File.Exists(HostPath)) host = Text(Json().Deserialize<Dictionary<string, object>>(File.ReadAllText(HostPath)), "ext_agent_host_id");
                else
                {
                    host = "urn:uuid:" + Guid.NewGuid().ToString();
                    File.WriteAllText(HostPath, Json().Serialize(new Dictionary<string, object> { { "ext_agent_host_id", host } }));
                }
                string state = RandomToken(), nonce = RandomToken(), verifier = RandomToken();
                string challenge;
                using (SHA256 sha = SHA256.Create()) challenge = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                try
                {
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    string redirect = "http://127.0.0.1:" + port + "/auth/callback";
                    string clientId = old == null ? "dynamic_agent_client" : Text(old, "client_id");
                    Dictionary<string, string> parameters = new Dictionary<string, string> {
                        { "client_id", clientId }, { "ext_agent_host_id", host }, { "response_type", "code" },
                        { "redirect_uri", redirect }, { "scope", Scope }, { "resource", Resource },
                        { "state", state }, { "nonce", nonce }, { "code_challenge_method", "S256" }, { "code_challenge", challenge }
                    };
                    if (old == null) parameters.Add("agent_name_hint", "Jarvis");
                    else if (!string.IsNullOrEmpty(Text(old, "email"))) parameters.Add("login_hint", Text(old, "email"));
                    Process.Start(new ProcessStartInfo(Issuer + "/api/accounts/authorize?" + Encode(parameters)) { UseShellExecute = true });
                    DateTime deadline = DateTime.UtcNow.AddMinutes(5);
                    while (DateTime.UtcNow < deadline)
                    {
                        if (!listener.Pending()) { Thread.Sleep(100); continue; }
                        using (TcpClient client = listener.AcceptTcpClient())
                        {
                            client.ReceiveTimeout = 5000;
                            client.SendTimeout = 5000;
                            using (NetworkStream stream = client.GetStream())
                            {
                                string requestLine = ReadLine(stream);
                                string line;
                                int headers = 0;
                                do { line = ReadLine(stream); if (++headers > 40) throw new Exception("Некорректный ответ браузера."); } while (!string.IsNullOrEmpty(line));
                                string[] request = requestLine.Split(' ');
                                Uri callback;
                                if (request.Length != 3 || request[0] != "GET" || !Uri.TryCreate("http://127.0.0.1:" + port + request[1], UriKind.Absolute, out callback) ||
                                    callback.AbsolutePath != "/auth/callback") { BrowserReply(stream, false, "Ожидается подтверждение входа."); continue; }
                                Dictionary<string, string> query = Query(callback.Query);
                                string returnedState;
                                if (!query.TryGetValue("state", out returnedState) || returnedState != state)
                                { BrowserReply(stream, false, "Это подтверждение относится к другому входу."); continue; }
                                try
                                {
                                    if (query.ContainsKey("error")) throw new Exception("Подключение не разрешено в браузере.");
                                    string issued;
                                    if (old == null)
                                    {
                                        if (!query.TryGetValue("client_id", out issued) || string.IsNullOrEmpty(issued) || issued == "dynamic_agent_client")
                                            throw new Exception("Регистрация аккаунта не завершена.");
                                        clientId = issued;
                                    }
                                    else if (query.TryGetValue("client_id", out issued) && issued != clientId)
                                        throw new Exception("Подтверждение относится к другому подключению.");
                                    string code;
                                    if (!query.TryGetValue("code", out code) || string.IsNullOrEmpty(code)) throw new Exception("Сервер не вернул подтверждение входа.");
                                    Dictionary<string, object> tokens = RequestJson(Issuer + "/api/accounts/oauth/token",
                                        new Dictionary<string, string> { { "grant_type", "authorization_code" }, { "client_id", clientId },
                                            { "code", code }, { "code_verifier", verifier }, { "redirect_uri", redirect }, { "resource", Resource } });
                                    Dictionary<string, object> identity = ValidateIdentity(Text(tokens, "id_token"), clientId, nonce);
                                    if (old != null && Text(identity, "sub") != Text(old, "subject"))
                                        throw new Exception("В браузере выбран другой аккаунт.");
                                    Dictionary<string, object> saved = new Dictionary<string, object> {
                                        { "email", Text(identity, "email") }, { "issuer", Issuer }, { "subject", Text(identity, "sub") },
                                        { "client_id", clientId }, { "ext_agent_host_id", host },
                                        { "id_token", Text(tokens, "id_token") }, { "access_token", Text(tokens, "access_token") },
                                        { "refresh_token", Text(tokens, "refresh_token") }, { "expires_in", tokens["expires_in"] },
                                        { "scopes", (Text(tokens, "scope") ?? "").Split(' ') }, { "saved_at", DateTime.UtcNow.ToString("o") }
                                    };
                                    CheckScopes(saved);
                                    if (string.IsNullOrEmpty(Text(saved, "access_token")) || string.IsNullOrEmpty(Text(saved, "refresh_token")))
                                        throw new Exception("Сервер вернул неполное подключение.");
                                    Save(saved, old != null);
                                    BrowserReply(stream, true, "Подписка ChatGPT подключена. Окно можно закрыть.");
                                    return Text(identity, "email");
                                }
                                catch (Exception error) { BrowserReply(stream, false, error.Message); throw; }
                            }
                        }
                    }
                    throw new Exception("Время ожидания входа истекло.");
                }
                finally { listener.Stop(); }
            }
            finally { gate.ReleaseMutex(); }
        }
    }
    static string ReadLine(Stream stream)
    {
        List<byte> bytes = new List<byte>();
        int next;
        while ((next = stream.ReadByte()) >= 0 && next != 10)
        {
            if (bytes.Count >= 16384) throw new Exception("Слишком длинный ответ браузера.");
            if (next != 13) bytes.Add((byte)next);
        }
        if (next < 0) throw new IOException("Browser connection closed.");
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
    static Dictionary<string, string> Query(string query)
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        foreach (string item in query.TrimStart('?').Split('&'))
        {
            string[] pair = item.Split(new char[] { '=' }, 2);
            string name = WebUtility.UrlDecode(pair[0]);
            if (values.ContainsKey(name)) throw new Exception("Повторяющийся параметр входа.");
            values.Add(name, pair.Length > 1 ? WebUtility.UrlDecode(pair[1]) : "");
        }
        return values;
    }
    static void BrowserReply(Stream stream, bool success, string text)
    {
        byte[] body = Encoding.UTF8.GetBytes(text);
        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + (success ? "200 OK" : "400 Bad Request") +
            "\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\nContent-Length: " + body.Length + "\r\n\r\n");
        stream.Write(header, 0, header.Length);
        stream.Write(body, 0, body.Length);
    }
}
