using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI
{
    public enum MenuShortcutCommand
    {
        None,
        Native,
        SelectAll,
        Copy,
        Cut,
        Paste,
        Undo,
        Redo
    }

    /// <summary>한 입력 필드의 사용자 편집 기록. 각 키 입력을 한 단계로 보존한다.</summary>
    public sealed class TextEditHistory
    {
        readonly List<string> _states = new();
        int _index;

        public TextEditHistory(string initialText)
        {
            _states.Add(initialText ?? string.Empty);
        }

        public void Record(string text)
        {
            string value = text ?? string.Empty;
            if (_states[_index] == value) return;

            int redoCount = _states.Count - _index - 1;
            if (redoCount > 0) _states.RemoveRange(_index + 1, redoCount);
            _states.Add(value);
            _index = _states.Count - 1;
        }

        public bool TryUndo(out string text)
        {
            if (_index == 0)
            {
                text = _states[0];
                return false;
            }

            _index--;
            text = _states[_index];
            return true;
        }

        public bool TryRedo(out string text)
        {
            if (_index >= _states.Count - 1)
            {
                text = _states[_index];
                return false;
            }

            _index++;
            text = _states[_index];
            return true;
        }
    }

    /// <summary>
    /// 런타임 생성 메뉴의 키보드 접근성 계층. TMP가 플랫폼 기본으로 제공하는 단축키는 중복 실행하지
    /// 않고, macOS의 Ctrl 계열과 undo/redo, Tab 포커스 이동만 보완한다.
    /// </summary>
    public sealed class MenuKeyboardShortcuts : MonoBehaviour
    {
        readonly List<Selectable> _focusOrder = new();
        readonly Dictionary<TMP_InputField, TextEditHistory> _histories = new();
        readonly Dictionary<TMP_InputField, UnityAction<string>> _historyListeners = new();
        readonly HashSet<TMP_InputField> _applyingHistory = new();

        public void Configure(params Selectable[] focusOrder)
        {
            UnsubscribeHistory();
            _focusOrder.Clear();

            if (focusOrder == null) return;
            foreach (var selectable in focusOrder)
            {
                if (selectable == null) continue;
                _focusOrder.Add(selectable);
                if (selectable is not TMP_InputField input) continue;

                _histories[input] = new TextEditHistory(input.text);
                UnityAction<string> listener = value =>
                {
                    if (!_applyingHistory.Contains(input)) _histories[input].Record(value);
                };
                _historyListeners[input] = listener;
                input.onValueChanged.AddListener(listener);
            }
        }

        void OnDestroy() => UnsubscribeHistory();

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                return;
            }

            TMP_InputField input = FocusedInput();
            if (input == null) return;

            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool command = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!control && !command) return;

            KeyCode key = PressedShortcutKey();
            if (key == KeyCode.None) return;

            bool isApple = Application.platform == RuntimePlatform.OSXEditor ||
                           Application.platform == RuntimePlatform.OSXPlayer;
            Apply(input, ResolveCommand(isApple, control, command, shift, key));
        }

        public static MenuShortcutCommand ResolveCommand(
            bool isApple,
            bool control,
            bool command,
            bool shift,
            KeyCode key)
        {
            bool actionModifier = control || command;
            if (!actionModifier) return MenuShortcutCommand.None;

            if (key == KeyCode.Z)
                return shift ? MenuShortcutCommand.Redo : MenuShortcutCommand.Undo;
            if (key == KeyCode.Y) return MenuShortcutCommand.Redo;

            bool nativeModifier = isApple ? command : control;
            if (nativeModifier) return MenuShortcutCommand.Native;

            return key switch
            {
                KeyCode.A => MenuShortcutCommand.SelectAll,
                KeyCode.C => MenuShortcutCommand.Copy,
                KeyCode.X => MenuShortcutCommand.Cut,
                KeyCode.V => MenuShortcutCommand.Paste,
                _ => MenuShortcutCommand.None
            };
        }

        static KeyCode PressedShortcutKey()
        {
            if (Input.GetKeyDown(KeyCode.Z)) return KeyCode.Z;
            if (Input.GetKeyDown(KeyCode.Y)) return KeyCode.Y;
            if (Input.GetKeyDown(KeyCode.A)) return KeyCode.A;
            if (Input.GetKeyDown(KeyCode.C)) return KeyCode.C;
            if (Input.GetKeyDown(KeyCode.X)) return KeyCode.X;
            if (Input.GetKeyDown(KeyCode.V)) return KeyCode.V;
            return KeyCode.None;
        }

        void Apply(TMP_InputField input, MenuShortcutCommand command)
        {
            switch (command)
            {
                case MenuShortcutCommand.SelectAll:
                    input.selectionStringAnchorPosition = 0;
                    input.selectionStringFocusPosition = input.text.Length;
                    input.ForceLabelUpdate();
                    break;
                case MenuShortcutCommand.Copy:
                    GUIUtility.systemCopyBuffer = SelectedText(input);
                    break;
                case MenuShortcutCommand.Cut:
                    GUIUtility.systemCopyBuffer = SelectedText(input);
                    ReplaceSelection(input, string.Empty);
                    break;
                case MenuShortcutCommand.Paste:
                    ReplaceSelection(input, GUIUtility.systemCopyBuffer ?? string.Empty);
                    break;
                case MenuShortcutCommand.Undo:
                    ApplyHistory(input, false);
                    break;
                case MenuShortcutCommand.Redo:
                    ApplyHistory(input, true);
                    break;
            }
        }

        void ApplyHistory(TMP_InputField input, bool redo)
        {
            if (!_histories.TryGetValue(input, out var history)) return;
            bool changed = redo ? history.TryRedo(out string text) : history.TryUndo(out text);
            if (!changed) return;

            _applyingHistory.Add(input);
            input.text = text;
            _applyingHistory.Remove(input);
            SetCaret(input, text.Length);
        }

        static string SelectedText(TMP_InputField input)
        {
            GetSelection(input, out int start, out int end);
            return end > start ? input.text.Substring(start, end - start) : string.Empty;
        }

        static void ReplaceSelection(TMP_InputField input, string replacement)
        {
            GetSelection(input, out int start, out int end);
            input.text = input.text.Remove(start, end - start).Insert(start, replacement);
            SetCaret(input, start + replacement.Length);
        }

        static void GetSelection(TMP_InputField input, out int start, out int end)
        {
            int length = input.text?.Length ?? 0;
            start = Mathf.Clamp(Math.Min(input.selectionStringAnchorPosition,
                input.selectionStringFocusPosition), 0, length);
            end = Mathf.Clamp(Math.Max(input.selectionStringAnchorPosition,
                input.selectionStringFocusPosition), 0, length);
        }

        static void SetCaret(TMP_InputField input, int position)
        {
            int clamped = Mathf.Clamp(position, 0, input.text?.Length ?? 0);
            input.selectionStringAnchorPosition = clamped;
            input.selectionStringFocusPosition = clamped;
            input.ForceLabelUpdate();
        }

        TMP_InputField FocusedInput()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.TryGetComponent(out TMP_InputField current) && current.isFocused)
                return current;

            foreach (var selectable in _focusOrder)
                if (selectable is TMP_InputField input && input.isFocused) return input;
            return null;
        }

        void MoveFocus(bool reverse)
        {
            var available = new List<Selectable>();
            foreach (var selectable in _focusOrder)
                if (selectable != null && selectable.IsActive() && selectable.interactable &&
                    selectable.gameObject.activeInHierarchy)
                    available.Add(selectable);

            if (available.Count == 0) return;
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            int index = available.FindIndex(item => item.gameObject == selected);
            index = index < 0
                ? (reverse ? available.Count - 1 : 0)
                : (index + (reverse ? -1 : 1) + available.Count) % available.Count;

            Selectable next = available[index];
            next.Select();
            if (next is TMP_InputField input) input.ActivateInputField();
        }

        void UnsubscribeHistory()
        {
            foreach (var pair in _historyListeners)
                if (pair.Key != null) pair.Key.onValueChanged.RemoveListener(pair.Value);
            _historyListeners.Clear();
            _histories.Clear();
            _applyingHistory.Clear();
        }
    }
}
