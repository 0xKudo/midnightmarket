using System;
using ArmsFair.Auth;
using ArmsFair.Network;
using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models.Messages;
using UnityEngine;
using UnityEngine.Events;

namespace ArmsFair.Game
{
    public class PhaseManager : MonoBehaviour
    {
        public static PhaseManager Instance { get; private set; }

        public UnityEvent<GamePhase, int> OnPhaseChanged = new();
        public UnityEvent<long>           OnTimerTick    = new();

        public GamePhase CurrentPhase     { get; private set; }
        public int       CurrentRound     { get; private set; }
        public long      EndsAtMs         { get; private set; }
        public long      TimeRemainingMs  => Math.Max(0, EndsAtMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        public bool IsMyTurnToAct =>
            CurrentPhase == GamePhase.Sales &&
            GameManager.Instance?.LocalPlayer?.Status == "active";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (GameClient.Instance == null) return;
            GameClient.Instance.OnPhaseStart.AddListener(HandlePhaseStart);
            GameClient.Instance.OnStateSync.AddListener(HandleStateSync);
        }

        private void OnDisable()
        {
            if (GameClient.Instance == null) return;
            GameClient.Instance.OnPhaseStart.RemoveListener(HandlePhaseStart);
            GameClient.Instance.OnStateSync.RemoveListener(HandleStateSync);
        }

        private void Update()
        {
            if (EndsAtMs > 0)
                OnTimerTick.Invoke(TimeRemainingMs);
        }

        private void HandlePhaseStart(PhaseStartMessage msg)
        {
            CurrentPhase = msg.Phase;
            CurrentRound = msg.Round;
            EndsAtMs     = msg.EndsAt;
            OnPhaseChanged.Invoke(CurrentPhase, CurrentRound);
        }

        private void HandleStateSync(StateSync msg)
        {
            // Only seed phase/round from state if PhaseStart hasn't arrived yet,
            // so that an authoritative PhaseStart always wins.
            if (EndsAtMs == 0)
            {
                CurrentPhase = msg.FullState.Phase;
                CurrentRound = msg.FullState.Round;
                OnPhaseChanged.Invoke(CurrentPhase, CurrentRound);
            }
        }
    }
}
