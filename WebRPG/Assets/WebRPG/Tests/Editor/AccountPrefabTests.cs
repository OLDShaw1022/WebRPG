using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證登入入口具備可編輯的固定欄位與主場景綁定。
    public sealed class AccountPrefabTests
    {
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator RegistrationReturnsToLoginOnlyAfterSuccess()
        {
            using AccountHttpTestServer server = new AccountHttpTestServer();
            using AccountModel model = new AccountModel(new LobbyClientConfiguration
            {
                DefaultServerId = "production", RequestTimeoutSeconds = 10,
                Servers = new[]
                {
                    new LobbyServerConfiguration { Id = "production", Name = "Production", Url = "wss://example.com/ws" },
                    new LobbyServerConfiguration { Id = "local", Name = "Local", Url = server.SocketUrl, EditorOnly = true }
                }
            }, true);
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/AccountCanvas.prefab"));
            try
            {
                AccountView view = instance.GetComponent<AccountView>();
                typeof(AccountView).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
                bool loggedIn = false;
                using AccountPresenter presenter = new AccountPresenter(model, view, () => loggedIn = true);
                Transform login = instance.transform.Find("LoginPage");
                Transform registration = instance.transform.Find("RegistrationPage");
                login.Find("OpenRegistration").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                registration.Find("Server").GetComponent<TMPro.TMP_Dropdown>().value = 1;
                registration.Find("Email").GetComponent<TMPro.TMP_InputField>().text = "player@example.com";
                registration.Find("Password").GetComponent<TMPro.TMP_InputField>().text = "short";
                registration.Find("Username").GetComponent<TMPro.TMP_InputField>().text = "玩家";
                registration.Find("Register").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                Assert.That(registration.gameObject.activeSelf, Is.True, "Validation errors must stay on registration");
                Assert.That(server.LastPath, Is.Null);
                registration.Find("Password").GetComponent<TMPro.TMP_InputField>().text = "long-enough-password";
                System.Threading.Tasks.Task response = server.RespondAsync(200, "{}");
                registration.Find("Register").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                while (!response.IsCompleted) yield return null;
                response.GetAwaiter().GetResult();
                while (!registration.Find("Register").GetComponent<UnityEngine.UI.Button>().interactable) yield return null;
                Assert.That(login.gameObject.activeSelf, Is.True);
                Assert.That(registration.gameObject.activeSelf, Is.False);
                Assert.That(login.Find("Email").GetComponent<TMPro.TMP_InputField>().text, Is.EqualTo("player@example.com"));
                Assert.That(login.Find("Status").GetComponent<TMPro.TextMeshProUGUI>().text, Does.Contain("註冊成功"));
                Assert.That(loggedIn, Is.False);
                Assert.That(model.IsLoggedIn, Is.False);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AccountPagesAreSeparateAndStartOnLogin()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/AccountCanvas.prefab");
            SerializedObject serialized = new SerializedObject(prefab.GetComponent<AccountView>());
            foreach (string field in new[] { "_loginPage", "_registrationPage", "_registrationEmail", "_registrationPassword", "_registrationServer", "_registrationStatus", "_openRegistration", "_backToLogin" })
            {
                SerializedProperty property = serialized.FindProperty(field);
                Assert.That(property, Is.Not.Null, field);
                Assert.That(property.objectReferenceValue, Is.Not.Null, field);
            }
            GameObject login = (GameObject)serialized.FindProperty("_loginPage").objectReferenceValue;
            GameObject registration = (GameObject)serialized.FindProperty("_registrationPage").objectReferenceValue;
            Assert.That(login, Is.Not.EqualTo(registration));
            Assert.That(login.activeSelf, Is.True);
            Assert.That(registration.activeSelf, Is.False);
            Assert.That(login.GetComponentsInChildren<TMPro.TMP_InputField>(true).Length, Is.EqualTo(2));
            Assert.That(registration.GetComponentsInChildren<TMPro.TMP_InputField>(true).Length, Is.EqualTo(3));
        }

        [Test]
        public void SwitchingPagesPreservesEmailAndServerAndClearsPasswords()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/AccountCanvas.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                AccountView view = instance.GetComponent<AccountView>();
                view.Configure(new[] { "Local", "Test" }, 0);
                Transform login = instance.transform.Find("LoginPage");
                Transform registration = instance.transform.Find("RegistrationPage");
                login.Find("Email").GetComponent<TMPro.TMP_InputField>().text = "player@example.com";
                login.Find("Password").GetComponent<TMPro.TMP_InputField>().text = "test-password";
                login.Find("Server").GetComponent<TMPro.TMP_Dropdown>().value = 1;
                view.ShowPage(true);
                Assert.That(login.gameObject.activeSelf, Is.False);
                Assert.That(registration.gameObject.activeSelf, Is.True);
                Assert.That(registration.Find("Email").GetComponent<TMPro.TMP_InputField>().text, Is.EqualTo("player@example.com"));
                Assert.That(registration.Find("Server").GetComponent<TMPro.TMP_Dropdown>().value, Is.EqualTo(1));
                Assert.That(login.Find("Password").GetComponent<TMPro.TMP_InputField>().text, Is.Empty);
                registration.Find("Password").GetComponent<TMPro.TMP_InputField>().text = "new-password";
                view.Render(true, "Pending");
                Assert.That(registration.Find("BackToLogin").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                Assert.That(registration.Find("Register").GetComponent<UnityEngine.UI.Button>().interactable, Is.False);
                registration.Find("Email").GetComponent<TMPro.TMP_InputField>().text = "registered@example.com";
                view.ShowPage(false);
                Assert.That(login.gameObject.activeSelf, Is.True);
                Assert.That(registration.gameObject.activeSelf, Is.False);
                Assert.That(login.Find("Email").GetComponent<TMPro.TMP_InputField>().text, Is.EqualTo("registered@example.com"));
                Assert.That(registration.Find("Password").GetComponent<TMPro.TMP_InputField>().text, Is.Empty);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AccountPrefabHasAllFormReferences()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WebRPG/Prefabs/AccountCanvas.prefab");
            Assert.That(prefab, Is.Not.Null);
            Component view = prefab.GetComponent("AccountView");
            Assert.That(view, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(view);
            foreach (string field in new[] { "_email", "_password", "_username", "_login", "_register", "_status", "_server" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
    }
}
