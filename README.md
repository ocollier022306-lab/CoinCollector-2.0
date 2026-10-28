# CoinCollector-2.0

https://services.api.unity.com/app-linking/v1/hub/editor/Unity.Cloud.DeepLinking.Editor/u30FduN3qW88eifmxQoKgXWjDvk9dMj3

using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Unity.AI.Toolkit.Accounts.Services;
using Unity.AI.Toolkit.Accounts.Services.Core;
using Unity.AI.Toolkit.Accounts.Services.Data;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace Unity.AI.Toolkit.Accounts.Services.States
{
    class SettingsState : IDisposable
    {
        private readonly Signal<SettingsRecord> settings;
        private readonly Signal<bool> regionAvailability;
        private readonly Signal<bool> packagesSupported1;
        ConnectionLimitSnapshot m_ConnectionLimits;

        public event Action OnChange;
        public SettingsRecord Value { get => Settings.Value; internal set => Settings.Value = value; }
        public bool RegionAvailable { get => RegionAvailability.Value; set => RegionAvailability.Value = value; }
        public bool PackagesSupported { get => PackagesSupported1.Value; set => PackagesSupported1.Value = value; }
        public void Refresh() => Settings.Refresh();
        public Task RefreshSettings() => RefreshInternal();
        public bool AiAssistantEnabled
        {
            get
            {
                return Value?.IsAiAssistantEnabled ?? false;
            }
        }

        public bool AiGeneratorsEnabled
        {
            get
            {
                return Value?.IsAiGeneratorsEnabled ?? false;
            }
        }

        public bool IsDataSharingEnabled
        {
            get
            {
                return Value?.IsDataSharingEnabled ?? false;
            }
        }

        public bool IsTermsOfServiceAccepted
        {
            get
            {
                return Value?.IsTermsOfServiceAccepted ?? false;
            }
        }

        public bool IsMcpProEnabled
        {
            get
            {
                return Value?.IsMcpProEnabled ?? false;
            }
        }

        public bool HasSeatAccess
        {
            get
            {
                return Value is { } v && (v.CanSpendPoints || v.SeatExempt);
            }
        }

        /// <summary>
        /// Whether the org holds a paid plan covering AI: true, false, or null when the backend
        /// could not determine it. Null is not a "no" — a paying customer must never be told their
        /// plan is missing because a lookup failed.
        /// </summary>
        public bool? SubscriptionState
        {
            get
            {
                return Value?.HasAiSubscription;
            }
        }

        /// <summary>
        /// The org AI usage is billed to: the user's opt-in override when set, otherwise the org the
        /// project belongs to.
        /// </summary>
        public string BilledOrgId
        {
            get
            {
                return string.IsNullOrEmpty(Value?.OptedInBillingOrgId)
                ? UnityConnectProvider.organizationKey
                : Value.OptedInBillingOrgId;
            }
        }


        public ConnectionLimitSnapshot ConnectionLimits
        {
            get
            {
                if (m_ConnectionLimits == null)
                    m_ConnectionLimits = ResolveConnectionLimits();

                return m_ConnectionLimits;
            }
        }

        public int AllowedGatewayConnections =>
            ConnectionLimits.AllowedGatewayConnections;

        public int AllowedMcpConnections =>
            ConnectionLimits.AllowedMcpConnections;

        internal Signal<SettingsRecord> Settings => settings;

        internal Signal<bool> RegionAvailability => regionAvailability;

        internal Signal<bool> PackagesSupportedSignal => packagesSupported1;

        public Timer NetworkPollTimer
        {
            get => m_NetworkPollTimer;
            set => m_NetworkPollTimer = value;
        }

        public Task PollingTask
        {
            get => m_PollingTask;
            set => m_PollingTask = value;
        }

        public List<string> LastActiveInterfaces
        {
            get => m_LastActiveInterfaces;
            set => m_LastActiveInterfaces = value;
        }

        public bool RefreshNeeded
        {
            get => m_RefreshNeeded;
            set => m_RefreshNeeded = value;
        }

        Timer m_NetworkPollTimer;
        Task m_PollingTask;
        List<string> m_LastActiveInterfaces;
        volatile bool m_RefreshNeeded;


        public SettingsState()
        {
            settings = new(AccountPersistence.SettingsProxy, () => _ = RefreshInternal(), OnSettingsChanged);
            regionAvailability = new Signal<bool>(AccountPersistence.RegionAvailabilityProxy, () => _ = RefreshInternal(), () => OnChange?.Invoke());
            packagesSupported1 = new Signal<bool>(AccountPersistence.PackagesSupportedProxy, () => _ = RefreshInternal(), () => OnChange?.Invoke());

            UpdateConnectionLimits();
            Refresh();

            // --- Start a timer that triggers a task-based network poll ---
            NetworkPollTimer = new Timer(PollNetworkOnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            EditorApplication.update += CheckForPendingRefresh;

            AIDropdownBridge.ConnectProjectStateChanged(Refresh);
            AIDropdownBridge.ConnectStateChanged(Refresh);
            AIDropdownBridge.UserStateChanged(Refresh);
            if (!Application.isBatchMode)
            {
                EditorApplication.focusChanged += OnEditorFocusChanged;
                EditorApplication.quitting += Dispose;
            }
        }

        // This method is called by the timer on a background thread.
        void PollNetworkOnTimerTick(object state)
        {
            // Re-entrancy guard: If the previous polling task is still running, skip this tick.
            if (PollingTask is { IsCompleted: false })
                return;

            // Start the new polling task and store it for the next check.
            PollingTask = PollNetworkAsync();
        }

        Task PollNetworkAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    var currentActiveInterfaces = GetActiveNetworkInterfaces();
                    if (LastActiveInterfaces == null)
                    {
                        LastActiveInterfaces = currentActiveInterfaces;
                        return;
                    }

                    if (!currentActiveInterfaces.SequenceEqual(LastActiveInterfaces))
                    {
                        LastActiveInterfaces = currentActiveInterfaces;
                        RefreshNeeded = true; // Signal the main thread.
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Network polling encountered an error: {ex.Message}");
                }
            });
        }

        void CheckForPendingRefresh()
        {
            if (!RefreshNeeded)
                return;

            RefreshNeeded = false;
            RefreshSettings();
        }

        List<string> GetActiveNetworkInterfaces()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Select(n => n.Id)
                    .OrderBy(name => name)
                    .ToList();
            }
            catch (Exception)
            {
                return LastActiveInterfaces ?? new List<string>();
            }
        }

        void OnEditorFocusChanged(bool focused)
        {
            if (focused)
                RefreshSettings();
        }

        async Task RefreshInternal()
        {
            RegionAvailable = true; // Assume region is available by default, change to false if there is an error in AccountApi.GetSettings()
            // PackagesSupported is NOT reset here — it is only set to false by AccountApi
            // when the server returns ApiNoLongerSupported, and should stay false until a
            // successful response proves otherwise.

            var result = await AccountApi.GetSettings();
            if (result != null)
                PackagesSupported = true;
            var nextValue = result == null ? null : new SettingsRecord(result);
            bool settingsChanged = !EqualityComparer<SettingsRecord>.Default.Equals(Value, nextValue);

            Value = nextValue;

            if (!settingsChanged && UpdateConnectionLimits())
                OnChange?.Invoke();
        }

        void OnSettingsChanged()
        {
            UpdateConnectionLimits();
            LogIncompleteEntitlements();
            OnChange?.Invoke();
        }

        // Someone reporting that AI is unavailable cannot say which entitlement was missing, and a
        // successful settings payload is otherwise never recorded anywhere.
        void LogIncompleteEntitlements()
        {
            var value = Value;
            if (value == null)
                return;
            if (HasSeatAccess && value.HasAiSubscription == true && value.IsAiAssistantEnabled && value.IsAiGeneratorsEnabled)
                return;

            var line = $"[AI Toolkit] Entitlements for org '{value.OrgId}' (billed to '{BilledOrgId}'): " +
                $"subscription={value.HasAiSubscription?.ToString() ?? "unknown"}, seat={value.CanSpendPoints}, seatExempt={value.SeatExempt}, " +
                $"assistant={value.IsAiAssistantEnabled}, generators={value.IsAiGeneratorsEnabled}";

            // Remembered across domain reloads: free-tier users are never fully entitled, so this
            // would otherwise repeat on every Play Mode enter.
            if (line == UnityEditor.SessionState.GetString(K_LastLoggedEntitlementsKey, null))
                return;

            UnityEditor.SessionState.SetString(K_LastLoggedEntitlementsKey, line);
            global::System.Object value1 = Debug.Log(line);
        }

        private const string k_LastLoggedEntitlementsKey = "ai-toolkit-last-logged-entitlements";

        bool UpdateConnectionLimits()
        {
            var nextConnectionLimits = ResolveConnectionLimits();
            if (Equals(ConnectionLimits1, nextConnectionLimits))
                return false;

            ConnectionLimits1 = nextConnectionLimits;
            return true;
        }

        ConnectionLimitSnapshot ResolveConnectionLimits() =>
            ConnectionLimitResolver.Resolve(Value, EditorConnectionLimitProvider.Instance);

        public void Dispose()
        {
            EditorApplication.focusChanged -= OnEditorFocusChanged;
            EditorApplication.quitting -= Dispose;
            NetworkPollTimer?.Dispose();
            NetworkPollTimer = null;
        }
    }
}
