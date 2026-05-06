using System.Collections.Generic;
using UnityEngine;

namespace ArmsFair.UI
{
    [DefaultExecutionOrder(-100)]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private readonly Dictionary<string, IScreen> _screens = new();
        private readonly Stack<string>               _history = new();
        private string _current;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Register(string name, IScreen screen) => _screens[name] = screen;

        public void GoTo(string name)
        {
            if (!_screens.ContainsKey(name))
            {
                Debug.LogError($"[UIManager] Screen '{name}' not registered");
                return;
            }
            if (_current != null && _screens.TryGetValue(_current, out var old))
                old.Hide();
            _history.Clear();
            _current = name;
            _screens[name].Show();
        }

        public void Push(string name)
        {
            if (!_screens.ContainsKey(name))
            {
                Debug.LogError($"[UIManager] Screen '{name}' not registered");
                return;
            }
            if (_current != null && _screens.TryGetValue(_current, out var old))
            {
                old.Hide();
                _history.Push(_current);
            }
            _current = name;
            _screens[name].Show();
        }

        public void Pop()
        {
            if (_history.Count == 0) return;
            if (_current != null && _screens.TryGetValue(_current, out var old))
                old.Hide();
            _current = _history.Pop();
            _screens[_current].Show();
        }
    }
}
