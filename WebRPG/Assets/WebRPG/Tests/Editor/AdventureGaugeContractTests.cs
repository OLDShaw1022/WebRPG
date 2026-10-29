using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace WebRPG.Tests
{
    // 驗證冒險畫面資料提供初始滿血的結構化生命值條。
    public sealed class AdventureGaugeContractTests
    {
        [Test]
        public void PresenterExposesInitialHealthAsStructuredFullGauge()
        {
            System.Reflection.PropertyInfo property = typeof(AdventureScreenModel).GetProperty("PlayerHealth");
            Assert.That(property, Is.Not.Null, "Structured health gauges have not been implemented.");
            AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(File.ReadAllText(
                Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
            AdventureModel model = new AdventureModel(configuration, 20);
            GaugeCaptureView view = new GaugeCaptureView();
            using (AdventurePresenter presenter = new AdventurePresenter(model, view))
            {
                object gauge = property.GetValue(view.Screen);
                Assert.That(gauge, Is.Not.Null);
                Assert.That(gauge.GetType().GetProperty("FillAmount").GetValue(gauge), Is.EqualTo(1f));
                Assert.That(gauge.GetType().GetProperty("Text").GetValue(gauge).ToString(),
                    Does.Contain(configuration.StartingHealth.ToString()));
            }
        }

        // 擷取冒險畫面資料，供數值條契約測試驗證。
        private sealed class GaugeCaptureView : IAdventureView
        {
            public event Action StartRequested
            {
                add { }
                remove { }
            }

            public event Action AbandonRequested
            {
                add { }
                remove { }
            }

            public event Action<int> ChoiceRequested
            {
                add { }
                remove { }
            }

            public event Action<string> CharacterFocusRequested
            {
                add { }
                remove { }
            }

            public event Action MapToggleRequested
            {
                add { }
                remove { }
            }

            public event Action<int> SkillRequested
            {
                add { }
                remove { }
            }

            public event Action<float> BattleSpeedRequested
            {
                add { }
                remove { }
            }

            public AdventureScreenModel Screen { get; private set; }

            public void Render(AdventureScreenModel screen)
            {
                Screen = screen;
            }
        }
    }
}
