#if VISTA
using System;
using System.Collections.Generic;
using System.Globalization;
using Pinwheel.Vista;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// Owns review-prompt creation, trigger decisions, and forced removal.
    /// </summary>
    [InitializeOnLoad]
    public static class ReviewPromptCoordinator
    {
        private const string REVIEWED_PREF_KEY = "Pinwheel.Vista.ReviewPrompt.Reviewed";
        private const string COOLDOWN_UNTIL_PREF_KEY = "Pinwheel.Vista.ReviewPrompt.CooldownUntilUtcTicks";
        private const string LAST_SCORE_PREF_KEY = "Pinwheel.Vista.ReviewPrompt.LastScore";
        private const int COOLDOWN_DAYS = 7;
        private const int ELIGIBILITY_SCORE_THRESHOLD = 3000;

        private static readonly List<ICanShowReviewPrompt> s_hosts;
        private static ReviewPrompt s_prompt;
        private static ICanShowReviewPrompt s_attachedHost;

        static ReviewPromptCoordinator()
        {
            s_hosts = new List<ICanShowReviewPrompt>();
            SuccessfulActionCounter.recorded += OnSuccessfulActionRecorded;
        }

        public static void ResetTracking()
        {
            DetachPrompt();
            SuccessfulActionCounter.Reset();
            EditorPrefs.DeleteKey(REVIEWED_PREF_KEY);
            EditorPrefs.DeleteKey(COOLDOWN_UNTIL_PREF_KEY);
            EditorPrefs.DeleteKey(LAST_SCORE_PREF_KEY);
        }

        public static void RegisterHost(ICanShowReviewPrompt host)
        {
            if (!IsHostAlive(host))
                return;

            UnregisterHost(host);
            s_hosts.Add(host);
        }

        public static void UnregisterHost(ICanShowReviewPrompt host)
        {
            if (host == null)
                return;

            if (ReferenceEquals(s_attachedHost, host))
            {
                DetachPrompt();
            }

            s_hosts.RemoveAll(entry => !IsHostAlive(entry) || ReferenceEquals(entry, host));
        }

        private static void OnSuccessfulActionRecorded(
            string actionKey,
            SuccessfulActionSnapshot snapshot)
        {
            if (HasAttachedPrompt())
                return;

            if (HasReviewed())
                return;

            if (IsInCooldown())
                return;

            if (!IsTriggerAction(actionKey))
                return;

            long currentScore = ComputeEligibilityScore(snapshot);
            long lastScore = GetLastScore();
            if (lastScore > currentScore)
            {
                lastScore = 0;
                EditorPrefs.SetString(
                    LAST_SCORE_PREF_KEY,
                    lastScore.ToString(CultureInfo.InvariantCulture));
            }

            if (currentScore - lastScore <= ELIGIBILITY_SCORE_THRESHOLD)
                return;

            ICanShowReviewPrompt host = FindActiveHost();
            if (host == null)
                return;

            AttachPrompt(host);
        }

        private static void AttachPrompt(ICanShowReviewPrompt host)
        {
            if (!IsHostAlive(host) || HasAttachedPrompt())
                return;

            ReviewPrompt prompt = new ReviewPrompt();
            prompt.SetTrackingLocation(UILocation.ReviewPrompt);
            prompt.reviewNowClicked += OnReviewNowClicked;
            prompt.laterClicked += OnLaterClicked;
            host.AttachReviewPrompt(prompt);
            if (prompt.parent == null)
            {
                prompt.reviewNowClicked -= OnReviewNowClicked;
                prompt.laterClicked -= OnLaterClicked;
                return;
            }

            s_prompt = prompt;
            s_attachedHost = host;
        }

        private static bool HasAttachedPrompt()
        {
            if (s_attachedHost == null)
                return false;

            if (IsHostAlive(s_attachedHost))
                return true;

            DetachPrompt();
            return false;
        }

        private static void DetachPrompt()
        {
            if (s_prompt != null)
            {
                s_prompt.reviewNowClicked -= OnReviewNowClicked;
                s_prompt.laterClicked -= OnLaterClicked;
                s_prompt.SetShown(false);
                s_prompt.RemoveFromHierarchy();
            }

            s_prompt = null;
            s_attachedHost = null;
        }

        private static void OnReviewNowClicked()
        {
            SaveCurrentScore();
            EditorPrefs.SetBool(REVIEWED_PREF_KEY, true);
            DetachPrompt();
        }

        private static void OnLaterClicked()
        {
            SaveCurrentScore();

            long cooldownUntilUtcTicks = DateTime.UtcNow
                .AddDays(COOLDOWN_DAYS)
                .Ticks;
            EditorPrefs.SetString(
                COOLDOWN_UNTIL_PREF_KEY,
                cooldownUntilUtcTicks.ToString(CultureInfo.InvariantCulture));
            DetachPrompt();
        }

        private static void SaveCurrentScore()
        {
            long currentScore = ComputeEligibilityScore(
                SuccessfulActionCounter.GetSnapshot());
            EditorPrefs.SetString(
                LAST_SCORE_PREF_KEY,
                currentScore.ToString(CultureInfo.InvariantCulture));
        }

        private static bool HasReviewed()
        {
            return EditorPrefs.GetBool(REVIEWED_PREF_KEY, false);
        }

        private static bool IsInCooldown()
        {
            string value = EditorPrefs.GetString(
                COOLDOWN_UNTIL_PREF_KEY,
                string.Empty);
            if (!long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long cooldownUntilUtcTicks))
                return false;

            return DateTime.UtcNow.Ticks < cooldownUntilUtcTicks;
        }

        private static long GetLastScore()
        {
            string value = EditorPrefs.GetString(
                LAST_SCORE_PREF_KEY,
                string.Empty);
            if (!long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long lastScore))
                return 0;

            return lastScore;
        }

        private static long ComputeEligibilityScore(
            SuccessfulActionSnapshot snapshot)
        {
            if (snapshot == null)
                return 0;

            long score = 0;
            for (int i = 0; i < snapshot.actions.Count; ++i)
            {
                SuccessfulActionCount action = snapshot.actions[i];
                int weight = 0;

                if (string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.BIOME_CREATED_FROM_TEMPLATE, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.TERRAIN_GRID_CREATED, StringComparison.Ordinal))
                {
                    weight = 100;
                }
                else if (string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.ASSET_CREATED, StringComparison.Ordinal))
                {
                    weight = 30;
                }
                else if (string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.QUICK_ACTION_INVOKED, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.GENERATE_ALL, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.UTIL_TOOL_INTERACTION, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.TERRAIN_SEAL_CREATED, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.GRAPH_SAVED, StringComparison.Ordinal))
                {
                    weight = 10;
                }
                else if (string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.GRAPH_EDITOR_INTERACTION, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.ASSET_INSPECTOR_INTERACTION, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.VISTA_MANAGER_INSPECTOR_INTERACTION, StringComparison.Ordinal) ||
                    string.Equals(action.actionId, SuccessfulActionCounter.ActionKeys.BIOME_INSPECTOR_INTERACTION, StringComparison.Ordinal))
                {
                    weight = 1;
                }

                score += (long)action.count * weight;
            }

            return score;
        }

        private static ICanShowReviewPrompt FindActiveHost()
        {
            for (int i = s_hosts.Count - 1; i >= 0; --i)
            {
                ICanShowReviewPrompt host = s_hosts[i];
                if (!IsHostAlive(host))
                {
                    s_hosts.RemoveAt(i);
                    continue;
                }

                if (host.isReviewPromptHostInFocus)
                    return host;
            }

            return null;
        }

        private static bool IsHostAlive(ICanShowReviewPrompt host)
        {
            if (host == null)
                return false;

            if (host is UnityEngine.Object unityObject)
                return unityObject != null;

            return true;
        }

        private static bool IsTriggerAction(string actionKey)
        {
            return string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.QUICK_ACTION_INVOKED, StringComparison.Ordinal) ||
                string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.TERRAIN_GRID_CREATED, StringComparison.Ordinal) ||
                string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.BIOME_CREATED_FROM_TEMPLATE, StringComparison.Ordinal) ||
                string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.TERRAIN_SEAL_CREATED, StringComparison.Ordinal) ||
                string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.ASSET_CREATED, StringComparison.Ordinal) ||
                string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.GENERATE_ALL, StringComparison.Ordinal) ||
                string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.UTIL_TOOL_INTERACTION, StringComparison.Ordinal) ||
                string.Equals(actionKey, SuccessfulActionCounter.ActionKeys.GRAPH_SAVED, StringComparison.Ordinal);
        }
    }
}
#endif
