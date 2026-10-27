using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace WebRPG.Tests
{
    public sealed class AdventurePresenterTests
    {
        [Test]
        public void ViewActionsReachModelAndDisposeUnsubscribes()
        {
            AdventureConfiguration configuration = JsonConvert.DeserializeObject<AdventureConfiguration>(File.ReadAllText(
                Path.Combine(Application.dataPath, "WebRPG/Data/AdventurePrototype.json")));
            AdventureModel model = new AdventureModel(configuration, 1);
            TestAdventureView view = new TestAdventureView();
            AdventurePresenter presenter = new AdventurePresenter(model, view);
            Assert.That(view.Screen.CanStart, Is.True);
            view.Start();
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Map));
            Assert.That(view.Screen.Skills.Length, Is.EqualTo(6));
            Assert.That(view.Screen.Choices.Length, Is.EqualTo(model.AvailableNodes.Count));
            view.Choose(0);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Battle));
            Assert.That(view.Screen.Choices, Is.Empty);
            view.Abandon();
            Assert.That(view.Screen.CanStart, Is.True);
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
            presenter.Dispose();
            presenter.Dispose();
            view.Start();
            Assert.That(model.Phase, Is.EqualTo(AdventurePhase.Failed));
        }

        private sealed class TestAdventureView : IAdventureView
        {
            public event Action StartRequested;
            public event Action AbandonRequested;
            public event Action<int> ChoiceRequested;
            public AdventureScreenModel Screen { get; private set; }

            public void Render(AdventureScreenModel screen)
            {
                Screen = screen;
            }

            public void Start()
            {
                StartRequested?.Invoke();
            }

            public void Abandon()
            {
                AbandonRequested?.Invoke();
            }

            public void Choose(int index)
            {
                ChoiceRequested?.Invoke(index);
            }
        }
    }
}
