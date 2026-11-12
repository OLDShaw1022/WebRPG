using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace WebRPG.Tests
{
    // 驗證啟動恢復登入、失效與網路失敗，以及可編輯的記住登入選項。
    public sealed class AccountRememberedClientTests
    {
        private const string LastLoginServerPreference = "WebRPG.Account.LastLoginServer";
        private const string SessionJson =
            "{\"accessToken\":\"restored-token\",\"accountId\":\"account-1\",\"username\":\"玩家\"," +
            "\"expiresAt\":\"2099-01-01T00:00:00Z\"}";

        [Test]
        public void RememberMeToggleIsBoundAndOptIn()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/AccountCanvas.prefab");
            SerializedProperty field = new SerializedObject(prefab.GetComponent<AccountView>())
                .FindProperty("_rememberMe");
            Assert.That(field, Is.Not.Null, "A serialized remember-me toggle is required");
            Assert.That(field.objectReferenceValue, Is.Not.Null);
            UnityEngine.UI.Toggle toggle = (UnityEngine.UI.Toggle)field.objectReferenceValue;
            Assert.That(toggle.isOn, Is.False);
            Assert.That(toggle.transform.parent.name, Is.EqualTo("LoginPage"));
        }

        [UnityTest]
        public IEnumerator RefreshRestoresIdentityWithoutPassword()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(
                AccountClientConfigurationHelper.Create(server.SocketUrl), true);
            Task response = server.RespondAsync(200, SessionJson);
            Awaitable<bool>.Awaiter restore = model.TryRestoreAsync(1).GetAwaiter();
            while (!restore.IsCompleted) yield return null;
            Assert.That(restore.GetResult(), Is.True);
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();
            Assert.That(model.IsLoggedIn, Is.True);
            Assert.That(model.AccountId, Is.EqualTo("account-1"));
            Assert.That(server.LastPath, Is.EqualTo("/api/accounts/refresh"));
            Assert.That(server.LastRequestHeader, Is.EqualTo("1"));
            Assert.That(server.LastBody, Is.Empty);
        }

        [UnityTest]
        public IEnumerator InGameRefreshSendsPreviousBearerAndUpdatesTheSameAccount()
        {
            const string initialCookie = "__Secure-WebRPGRemember=initial";
            const string renewedCookie = "__Secure-WebRPGRemember=renewed";
            const string cookieAttributes = "; Path=/api/accounts; HttpOnly";
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(
                AccountClientConfigurationHelper.Create(server.SocketUrl), true);
            Task response = server.RespondAsync(200, SessionJson, initialCookie + cookieAttributes);
            Awaitable<string>.Awaiter login = model.SubmitAsync(1, "player@example.invalid", "test-password",
                string.Empty, false, true).GetAwaiter();
            while (!login.IsCompleted) yield return null;
            login.GetResult();
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();

            response = server.RespondAsync(200, SessionJson.Replace("restored-token", "renewed-token"),
                renewedCookie + cookieAttributes);
            var refreshMethod = typeof(AccountModel).GetMethod("TryRefreshSessionAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var refresh = ((Awaitable<bool>)refreshMethod.Invoke(model, null)).GetAwaiter();
            while (!refresh.IsCompleted) yield return null;
            Assert.That(refresh.GetResult(), Is.True);
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();

            Assert.That(server.LastPath, Is.EqualTo("/api/accounts/refresh"));
            Assert.That(server.LastAuthorization, Is.EqualTo("Bearer restored-token"));
            Assert.That(server.LastCookie, Is.EqualTo(initialCookie));
            Assert.That(model.AccessToken, Is.EqualTo("renewed-token"));
            Assert.That(model.AccountId, Is.EqualTo("account-1"));
        }

        [UnityTest]
        public IEnumerator ClearingExpiredSessionCancelsPendingRefresh()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(
                AccountClientConfigurationHelper.Create(server.SocketUrl), true);
            Task response = server.RespondAsync(200, SessionJson);
            var restore = model.TryRestoreAsync(1).GetAwaiter();
            while (!restore.IsCompleted) yield return null;
            Assert.That(restore.GetResult(), Is.True);
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();

            var refreshMethod = typeof(AccountModel).GetMethod("TryRefreshSessionAsync",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var refresh = ((Awaitable<bool>)refreshMethod.Invoke(model, null)).GetAwaiter();
            model.ClearSession();
            while (!refresh.IsCompleted) yield return null;
            Assert.Catch<OperationCanceledException>(() => refresh.GetResult());
            Assert.That(model.IsLoggedIn, Is.False);
            Assert.That(model.AccessToken, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RotatedCookieRestoresAcrossNewPlaySessionsUntilLogout()
        {
            const string first = "__Secure-WebRPGRemember=initial";
            const string second = "__Secure-WebRPGRemember=rotated";
            const string cookiePath = "; Path=/api/accounts; HttpOnly";
            using AccountHttpTestServer server = new AccountHttpTestServer();
            LobbyClientConfiguration configuration = AccountClientConfigurationHelper.Create(server.SocketUrl);

            using (AccountModel model = new AccountModel(configuration, true))
            {
                Task response = server.RespondAsync(200, SessionJson, first + cookiePath);
                Awaitable<string>.Awaiter login = model.SubmitAsync(1, "player@example.invalid", "test-password",
                    string.Empty, false, true).GetAwaiter();
                while (!login.IsCompleted) yield return null;
                login.GetResult();
                while (!response.IsCompleted) yield return null;
                response.GetAwaiter().GetResult();
            }

            using (AccountModel model = new AccountModel(configuration, true))
            {
                Task response = server.RespondAsync(200, SessionJson, second + cookiePath);
                Awaitable<bool>.Awaiter restore = model.TryRestoreAsync(1).GetAwaiter();
                while (!restore.IsCompleted) yield return null;
                Assert.That(restore.GetResult(), Is.True);
                while (!response.IsCompleted) yield return null;
                response.GetAwaiter().GetResult();
                Assert.That(server.LastCookie, Is.EqualTo(first));
            }

            using (AccountModel model = new AccountModel(configuration, true))
            {
                Task response = server.RespondAsync(200, SessionJson, second + cookiePath);
                Awaitable<bool>.Awaiter restore = model.TryRestoreAsync(1).GetAwaiter();
                while (!restore.IsCompleted) yield return null;
                Assert.That(restore.GetResult(), Is.True);
                while (!response.IsCompleted) yield return null;
                response.GetAwaiter().GetResult();
                Assert.That(server.LastCookie, Is.EqualTo(second));

                response = server.RespondAsync(204, string.Empty,
                    "__Secure-WebRPGRemember=; Expires=Thu, 01 Jan 1970 00:00:00 GMT" + cookiePath);
                Awaitable.Awaiter logout = model.LogoutAsync().GetAwaiter();
                while (!logout.IsCompleted) yield return null;
                logout.GetResult();
                while (!response.IsCompleted) yield return null;
                response.GetAwaiter().GetResult();
                Assert.That(server.LastCookie, Is.EqualTo(second));
            }

            using (AccountModel model = new AccountModel(configuration, true))
            {
                Task response = server.RespondAsync(401, string.Empty);
                Awaitable<bool>.Awaiter restore = model.TryRestoreAsync(1).GetAwaiter();
                while (!restore.IsCompleted) yield return null;
                Assert.That(restore.GetResult(), Is.False);
                while (!response.IsCompleted) yield return null;
                response.GetAwaiter().GetResult();
                Assert.That(server.LastCookie, Is.Null.Or.Empty);
            }
        }

        [UnityTest]
        public IEnumerator RememberedLoginRestoresFromTheLastLoginServer()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            LobbyClientConfiguration configuration = AccountClientConfigurationHelper.Create(server.SocketUrl);
            string previousServerId = PlayerPrefs.HasKey(LastLoginServerPreference)
                ? PlayerPrefs.GetString(LastLoginServerPreference)
                : null;
            GameObject instance = UnityEngine.Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/AccountCanvas.prefab"));
            try
            {
                AccountView view = instance.GetComponent<AccountView>();
                PlayerPrefs.DeleteKey(LastLoginServerPreference);
                using AccountModel model = new AccountModel(configuration, true);
                typeof(AccountView)
                    .GetMethod(
                    "Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(view, null);
                bool loggedIn = false;
                using (AccountPresenter presenter = new AccountPresenter(model, view, () => loggedIn = true))
                {
                    Transform login = instance.transform.Find("LoginPage");
                    TMPro.TMP_Dropdown serverDropdown = login.Find("Server").GetComponent<TMPro.TMP_Dropdown>();
                    Assert.That(serverDropdown.value, Is.Zero);
                    serverDropdown.value = 1;
                    login.Find("Email").GetComponent<TMPro.TMP_InputField>().text = "player@example.invalid";
                    login.Find("Password").GetComponent<TMPro.TMP_InputField>().text = "long-enough-password";
                    login.Find("RememberMe").GetComponent<UnityEngine.UI.Toggle>().isOn = true;
                    Task loginResponse = server.RespondAsync(200, SessionJson);
                    login.Find("Login").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(login.Find("RememberMe").GetComponent<UnityEngine.UI.Toggle>().interactable, Is.False);
                    while (!loginResponse.IsCompleted) yield return null;
                    loginResponse.GetAwaiter().GetResult();
                    while (!login.Find("Login").GetComponent<UnityEngine.UI.Button>().interactable) yield return null;
                    Assert.That(loggedIn, Is.True);
                    Assert.That(Newtonsoft.Json.Linq.JObject.Parse(server.LastBody).Value<bool>("rememberMe"), Is.True);
                    Assert.That(server.LastRequestHeader, Is.EqualTo("1"));
                    Assert.That(login.Find("Password").GetComponent<TMPro.TMP_InputField>().text, Is.Empty);
                }

                using AccountModel restartedModel = new AccountModel(configuration, true);
                loggedIn = false;
                using AccountPresenter restartedPresenter = new AccountPresenter(restartedModel, view,
                    () => loggedIn = true);
                Assert.That(instance.transform.Find("LoginPage/Server").GetComponent<TMPro.TMP_Dropdown>().value,
                    Is.EqualTo(1));
                Task response = server.RespondAsync(200, SessionJson);
                Awaitable.Awaiter restore = restartedPresenter.RestoreAsync().GetAwaiter();
                Assert.That(
                    instance.transform.Find("LoginPage/Login").GetComponent<UnityEngine.UI.Button>().interactable,
                    Is.False);
                while (!restore.IsCompleted) yield return null;
                restore.GetResult();
                Assert.That(loggedIn, Is.True);
                while (!response.IsCompleted) yield return null;
                response.GetAwaiter().GetResult();
                Assert.That(restartedModel.IsLoggedIn, Is.True);
                Assert.That(restartedModel.ServerIndex, Is.EqualTo(1));
                Assert.That(server.LastPath, Is.EqualTo("/api/accounts/refresh"));
            }
            finally
            {
                if (previousServerId == null) PlayerPrefs.DeleteKey(LastLoginServerPreference);
                else PlayerPrefs.SetString(LastLoginServerPreference, previousServerId);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [UnityTest]
        public IEnumerator MissingOrExpiredCookieReturnsToLogin()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(
                AccountClientConfigurationHelper.Create(server.SocketUrl), true);
            Task response = server.RespondAsync(401, string.Empty);
            Awaitable<bool>.Awaiter restore = model.TryRestoreAsync(1).GetAwaiter();
            while (!restore.IsCompleted) yield return null;
            Assert.That(restore.GetResult(), Is.False);
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();
            Assert.That(model.IsLoggedIn, Is.False);
        }

        [UnityTest]
        public IEnumerator UnavailableRefreshServiceDoesNotCreateSession()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(
                AccountClientConfigurationHelper.Create(server.SocketUrl), true);
            Task response = server.RespondAsync(503, "{}");
            Awaitable<bool>.Awaiter restore = model.TryRestoreAsync(1).GetAwaiter();
            while (!restore.IsCompleted) yield return null;
            Assert.Throws<InvalidOperationException>(() => restore.GetResult());
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();
            Assert.That(model.IsLoggedIn, Is.False);
        }
    }
}
