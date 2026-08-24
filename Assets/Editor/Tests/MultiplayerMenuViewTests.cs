using System.Reflection;
using Game.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MultiplayerMenuViewTests
{
    [Test]
    public void EnsureEventSystem_RestoresMenuSystemAfterPreviewDisablesItsSystem()
    {
        var host = new GameObject("MultiplayerMenuViewTests_EventHost");
        var menuSystemObject = new GameObject(
            "MultiplayerMenu_EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        var previewSystemObject = new GameObject(
            "Preview_EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));

        try
        {
            var menu = host.AddComponent<MultiplayerMenu>();
            typeof(MultiplayerMenu).GetField(
                    "_eventSystemGo",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(menu, menuSystemObject);

            var menuSystem = menuSystemObject.GetComponent<EventSystem>();
            var menuInput = menuSystemObject.GetComponent<StandaloneInputModule>();
            var previewSystem = previewSystemObject.GetComponent<EventSystem>();

            previewSystem.enabled = false;
            menuSystem.enabled = false;
            menuInput.enabled = false;

            typeof(MultiplayerMenu).GetMethod(
                    "EnsureEventSystem",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(menu, null);

            Assert.That(menuSystem.enabled, Is.True);
            Assert.That(menuInput.enabled, Is.True);
            Assert.That(previewSystem.enabled, Is.False,
                "The menu must recover its own input without re-enabling the preview scene's EventSystem.");
        }
        finally
        {
            Object.DestroyImmediate(previewSystemObject);
            Object.DestroyImmediate(menuSystemObject);
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void HorrorFont_LoadsBundledBaskervilleTypeface()
    {
        var font = MultiplayerMenu.HorrorFont;

        Assert.That(font, Is.Not.Null);
        StringAssert.Contains("BASKERVILLE", font.name.ToUpperInvariant());
    }

    [Test]
    public void BuildUi_MainActionsUseRestrainedHorrorPresentation()
    {
        var host = new GameObject("MultiplayerMenuViewTests_Host");
        host.SetActive(false);
        try
        {
            var menu = host.AddComponent<MultiplayerMenu>();
            typeof(MultiplayerMenu).GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(menu, null);

            var create = FindComponent<Button>(host.transform, "ShowCreateButton");
            var join = FindComponent<Button>(host.transform, "ShowJoinButton");
            var quit = FindComponent<Button>(host.transform, "QuitButton");

            Assert.That(create.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Create Room"));
            Assert.That(join.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Join Room"));
            Assert.That(quit.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Quit"));

            Assert.That(MaxRgb(create.colors.normalColor), Is.LessThan(0.16f));
            Assert.That(create.colors.normalColor, Is.EqualTo(join.colors.normalColor));
            Assert.That(create.colors.normalColor, Is.EqualTo(quit.colors.normalColor));
            Assert.That(create.colors.highlightedColor.r,
                Is.GreaterThan(create.colors.highlightedColor.g + 0.08f));

            var status = FindComponent<TMP_Text>(host.transform, "StatusText");
            Assert.That(ChannelSpread(status.color), Is.LessThan(0.1f));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BuildUi_UsesIndividualButtonBordersWithoutAnOuterFrame()
    {
        var host = new GameObject("MultiplayerMenuViewTests_Host");
        host.SetActive(false);
        try
        {
            var menu = host.AddComponent<MultiplayerMenu>();
            typeof(MultiplayerMenu).GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(menu, null);

            var panel = FindGameObject(host.transform, "Panel");
            Assert.That(FindGameObjectOrNull(host.transform, "PanelBackdrop"), Is.Null);
            Assert.That(panel.GetComponent<Image>(), Is.Null);
            Assert.That(panel.GetComponent<Outline>(), Is.Null);

            var buttons = host.GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Is.Not.Empty);
            foreach (var button in buttons)
            {
                var outline = button.GetComponent<Outline>();
                Assert.That(outline, Is.Not.Null, $"{button.name} should have its own border.");
                Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(1f, -1f)));
                Assert.That(outline.effectColor.a, Is.GreaterThanOrEqualTo(0.6f));
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BuildUi_LobbyActionsShareOneRowInLeaveThenStartOrder()
    {
        var host = new GameObject("MultiplayerMenuViewTests_Host");
        host.SetActive(false);
        try
        {
            var menu = host.AddComponent<MultiplayerMenu>();
            typeof(MultiplayerMenu).GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(menu, null);

            var actions = FindGameObject(host.transform, "SessionActions");
            var layout = actions.GetComponent<HorizontalLayoutGroup>();
            Assert.That(layout, Is.Not.Null);
            Assert.That(actions.transform.childCount, Is.EqualTo(2));
            Assert.That(actions.transform.GetChild(0).name, Is.EqualTo("LeaveButton"));
            Assert.That(actions.transform.GetChild(1).name, Is.EqualTo("StartButton"));

            var leaveLayout = actions.transform.GetChild(0).GetComponent<LayoutElement>();
            var startLayout = actions.transform.GetChild(1).GetComponent<LayoutElement>();
            Assert.That(leaveLayout.preferredHeight, Is.EqualTo(44f));
            Assert.That(startLayout.preferredHeight, Is.EqualTo(44f));
            Assert.That(leaveLayout.flexibleWidth, Is.EqualTo(1f));
            Assert.That(startLayout.flexibleWidth, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BuildUi_RoomCodeStaysOnOneLineAndShrinksBeforeClipping()
    {
        var host = new GameObject("MultiplayerMenuViewTests_Host");
        host.SetActive(false);
        try
        {
            var menu = host.AddComponent<MultiplayerMenu>();
            typeof(MultiplayerMenu).GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(menu, null);

            var roomCode = FindComponent<TextMeshProUGUI>(host.transform, "RoomCodeText");
            Assert.That(roomCode.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(roomCode.enableAutoSizing, Is.True);
            Assert.That(roomCode.fontSizeMax, Is.EqualTo(44f));
            Assert.That(roomCode.fontSizeMin, Is.LessThanOrEqualTo(32f));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BuildUi_InstallsKeyboardShortcutsForEveryRuntimeMenuField()
    {
        var host = new GameObject("MultiplayerMenuViewTests_Host");
        host.SetActive(false);
        try
        {
            var menu = host.AddComponent<MultiplayerMenu>();
            typeof(MultiplayerMenu).GetMethod("BuildUi", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(menu, null);

            var shortcutType = typeof(MultiplayerMenu).Assembly.GetType("Game.UI.MenuKeyboardShortcuts");
            Assert.That(shortcutType, Is.Not.Null);

            var shortcut = host.GetComponentInChildren(shortcutType, true);
            Assert.That(shortcut, Is.Not.Null,
                "The runtime-created menu canvas must own one shared keyboard shortcut handler.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [TestCase(HomeMenuView.Main)]
    [TestCase(HomeMenuView.CreateRoom)]
    [TestCase(HomeMenuView.JoinRoom)]
    public void VisibleViewForTests_WithoutSession_ShowsRequestedMainMenuView(HomeMenuView requested)
        => Assert.That(MultiplayerMenu.VisibleViewForTests(false, requested), Is.EqualTo(requested));

    [Test]
    public void VisibleViewForTests_SessionOverridesJoinCardWithLobby()
        => Assert.That(MultiplayerMenu.VisibleViewForTests(true, HomeMenuView.JoinRoom), Is.EqualTo(HomeMenuView.Lobby));

    static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        foreach (var component in root.GetComponentsInChildren<T>(true))
            if (component.gameObject.name == objectName)
                return component;

        Assert.Fail($"Expected to find {typeof(T).Name} on {objectName}.");
        return null;
    }

    static GameObject FindGameObject(Transform root, string objectName)
    {
        var found = FindGameObjectOrNull(root, objectName);
        Assert.That(found, Is.Not.Null, $"Expected to find GameObject {objectName}.");
        return found;
    }

    static GameObject FindGameObjectOrNull(Transform root, string objectName)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.gameObject.name == objectName)
                return child.gameObject;

        return null;
    }

    static float MaxRgb(Color color) => Mathf.Max(color.r, Mathf.Max(color.g, color.b));

    static float ChannelSpread(Color color)
        => MaxRgb(color) - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
}
