using System;
using System.Collections;
using System.Reflection;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuKeyboardShortcutsTests
{
    GameObject _root;

    [TearDown]
    public void TearDown()
    {
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        ClearEventSystems();
    }

    [Test]
    public void TextHistory_UndoReturnsEarlierUserEdits()
    {
        object history = CreateHistory(string.Empty);
        Invoke(history, "Record", "a");
        Invoke(history, "Record", "ab");

        Assert.That(TryHistory(history, "TryUndo", out string first), Is.True);
        Assert.That(first, Is.EqualTo("a"));
        Assert.That(TryHistory(history, "TryUndo", out string second), Is.True);
        Assert.That(second, Is.EqualTo(string.Empty));
        Assert.That(TryHistory(history, "TryUndo", out _), Is.False);
    }

    [Test]
    public void TextHistory_NewEditAfterUndoClearsRedoBranch()
    {
        object history = CreateHistory(string.Empty);
        Invoke(history, "Record", "a");
        Invoke(history, "Record", "ab");
        Assert.That(TryHistory(history, "TryUndo", out _), Is.True);

        Invoke(history, "Record", "ax");

        Assert.That(TryHistory(history, "TryRedo", out _), Is.False);
        Assert.That(TryHistory(history, "TryUndo", out string previous), Is.True);
        Assert.That(previous, Is.EqualTo("a"));
    }

    [TestCase(true, true, false, false, KeyCode.A, "SelectAll")]
    [TestCase(true, true, false, false, KeyCode.V, "Paste")]
    [TestCase(true, false, true, false, KeyCode.A, "Native")]
    [TestCase(true, false, true, false, KeyCode.Z, "Undo")]
    [TestCase(true, false, true, true, KeyCode.Z, "Redo")]
    [TestCase(true, true, false, false, KeyCode.Z, "Undo")]
    [TestCase(false, true, false, false, KeyCode.Z, "Undo")]
    [TestCase(true, false, false, false, KeyCode.A, "None")]
    public void ResolveCommand_SupportsControlAndCommandWithoutDuplicatingTmpNativeEdits(
        bool isApple,
        bool control,
        bool command,
        bool shift,
        KeyCode key,
        string expected)
    {
        Type type = FindRuntimeType("Game.UI.MenuKeyboardShortcuts");
        MethodInfo method = type?.GetMethod(
            "ResolveCommand",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null,
            "The shortcut router must distinguish supplemental Ctrl shortcuts from TMP's native platform shortcuts.");

        object actual = method.Invoke(null, new object[] { isApple, control, command, shift, key });
        Assert.That(actual.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void MoveFocus_AdvancesExactlyOneAvailableControl()
    {
        ClearEventSystems();
        _root = new GameObject("ShortcutTestRoot");
        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        eventSystemObject.transform.SetParent(_root.transform);
        var eventSystem = eventSystemObject.GetComponent<EventSystem>();
        typeof(EventSystem).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(eventSystem, null);

        var first = CreateButton("First");
        var hidden = CreateButton("Hidden");
        var second = CreateButton("Second");
        hidden.gameObject.SetActive(false);

        var shortcuts = _root.AddComponent<MenuKeyboardShortcuts>();
        shortcuts.Configure(first, hidden, second);
        eventSystem.SetSelectedGameObject(first.gameObject);

        MethodInfo moveFocus = typeof(MenuKeyboardShortcuts).GetMethod(
            "MoveFocus",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(moveFocus, Is.Not.Null);
        moveFocus.Invoke(shortcuts, new object[] { false });

        Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(second.gameObject));
    }

    static object CreateHistory(string initial)
    {
        Type type = FindRuntimeType("Game.UI.TextEditHistory");
        Assert.That(type, Is.Not.Null, "Menu text fields need a real undo/redo history.");
        return Activator.CreateInstance(type, initial);
    }

    static Type FindRuntimeType(string fullName)
        => typeof(MultiplayerMenu).Assembly.GetType(fullName);

    static object Invoke(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, args);
    }

    static bool TryHistory(object history, string methodName, out string value)
    {
        object[] args = { null };
        bool result = (bool)Invoke(history, methodName, args);
        value = (string)args[0];
        return result;
    }

    Button CreateButton(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_root.transform);
        return go.GetComponent<Button>();
    }

    static void ClearEventSystems()
    {
        var field = typeof(EventSystem).GetField(
            "m_EventSystems",
            BindingFlags.NonPublic | BindingFlags.Static);
        (field?.GetValue(null) as IList)?.Clear();
    }
}
