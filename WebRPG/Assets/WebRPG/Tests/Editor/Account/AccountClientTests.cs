using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WebRPG.Tests
{
    // 驗證帳號 HTTP 錯誤處理、登入身分與登出憑證撤銷要求。
    public sealed class AccountClientTests
    {
        [UnityTest]
        public IEnumerator LoginThenLogoutUsesBearerAndClearsSession()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(Configuration(server.SocketUrl), true);
            Task response = server.RespondAsync(200, "{\"accessToken\":\"test-token\",\"accountId\":\"account-1\",\"username\":\"同名玩家\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}");
            Awaitable<string>.Awaiter login = model.SubmitAsync(1, "player@example.com", "long-enough-password", string.Empty, false).GetAwaiter();
            while (!login.IsCompleted) yield return null;
            Assert.That(login.GetResult(), Is.Empty);
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();
            Assert.That(response.IsCompletedSuccessfully, Is.True);
            Assert.That(model.IsLoggedIn, Is.True);
            Assert.That(model.AccountId, Is.EqualTo("account-1"));
            Assert.That(server.LastPath, Is.EqualTo("/api/accounts/login"));
            response = server.RespondAsync(204, string.Empty);
            Awaitable.Awaiter logout = model.LogoutAsync().GetAwaiter();
            while (!logout.IsCompleted) yield return null;
            logout.GetResult();
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();
            Assert.That(response.IsCompletedSuccessfully, Is.True);
            Assert.That(server.LastAuthorization, Is.EqualTo("Bearer test-token"));
            Assert.That(server.LastPath, Is.EqualTo("/api/accounts/logout"));
            Assert.That(model.IsLoggedIn, Is.False);
            Assert.That(model.AccessToken, Is.Empty);
            Assert.That(model.AccountId, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FailedLoginShowsServerErrorWithoutCreatingSession()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(Configuration(server.SocketUrl), true);
            Task response = server.RespondAsync(401, "{\"error\":\"Email 或密碼錯誤。\"}");
            Awaitable<string>.Awaiter login = model.SubmitAsync(1, "player@example.com", "incorrect-password", string.Empty, false).GetAwaiter();
            while (!login.IsCompleted) yield return null;
            Exception error = Assert.Throws<InvalidOperationException>(() => login.GetResult());
            Assert.That(error.Message, Is.EqualTo("Email 或密碼錯誤。"));
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();
            Assert.That(response.IsCompletedSuccessfully, Is.True);
            Assert.That(model.IsLoggedIn, Is.False);
        }

        [UnityTest]
        public IEnumerator RegistrationRequiresAtLeastEightPasswordCharacters()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(Configuration(server.SocketUrl), true);
            Awaitable<string>.Awaiter rejected = model.SubmitAsync(1, "player@example.com", "1234567", "玩家", true).GetAwaiter();
            while (!rejected.IsCompleted) yield return null;
            Assert.That(rejected.GetResult(), Does.Contain("8～128"));
            Assert.That(server.LastPath, Is.Null);
            Task response = server.RespondAsync(200, "{}");
            Awaitable<string>.Awaiter accepted = model.SubmitAsync(1, "player@example.com", "12345678", "玩家", true).GetAwaiter();
            while (!accepted.IsCompleted) yield return null;
            Assert.That(accepted.GetResult(), Is.EqualTo("註冊成功，請使用 Email 與密碼登入。"));
            while (!response.IsCompleted) yield return null;
            response.GetAwaiter().GetResult();
            Assert.That(server.LastPath, Is.EqualTo("/api/accounts/register"));
        }

        private static LobbyClientConfiguration Configuration(string socketUrl)
        {
            return new LobbyClientConfiguration
            {
                DefaultServerId = "production", RequestTimeoutSeconds = 10,
                Servers = new[]
                {
                    new LobbyServerConfiguration { Id = "production", Name = "Production", Url = "wss://example.com/ws" },
                    new LobbyServerConfiguration { Id = "test", Name = "Local", Url = socketUrl, EditorOnly = true }
                }
            };
        }
    }
}
