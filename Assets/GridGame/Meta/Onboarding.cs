using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>GG-T1392: the onboarding step a save is on, in order: the opening crawl, the tutorial drills, Camp's first
    /// visit, done.</summary>
    public enum OnboardingStep { Crawl, Drill, HubIntro, Done }

    /// <summary>GG-T1392: where a new campaign's onboarding stands (<see cref="SaveData.Onboarding"/>). Save v25: an older
    /// save is <see cref="OnboardingStep.Done"/>.</summary>
    public sealed class OnboardingSave
    {
        public OnboardingStep Step;
        /// <summary>The drill's index in campaign.json Tutorial while <see cref="Step"/> is <see cref="OnboardingStep.Drill"/>.</summary>
        public int Drill;
    }

    /// <summary>The screen a save resumes at: <see cref="Step"/>, and for a drill its index and stage id.</summary>
    public readonly struct OnboardingResume
    {
        public readonly OnboardingStep Step;
        public readonly int Drill;
        public readonly string StageId;

        public OnboardingResume(OnboardingStep step, int drill = 0, string stageId = null)
        {
            Step = step;
            Drill = drill;
            StageId = stageId;
        }
    }

    /// <summary>
    /// GG-T1392: the onboarding checkpoint. Each step writes itself as it starts (<see cref="Begin"/>); a Continue or Load
    /// asks <see cref="Next"/> which screen to resume. A battle is never saved mid-fight (BIBLE 26.8), so a drill resumes
    /// from its start.
    /// </summary>
    public static class Onboarding
    {
        /// <summary>Records <paramref name="step"/> (with its drill index) as started. Never moves a finished onboarding back.</summary>
        public static void Begin(SaveData s, OnboardingStep step, int drill = 0)
        {
            if (s == null) return;
            s.Onboarding ??= new OnboardingSave();
            if (s.Onboarding.Step == OnboardingStep.Done) return;
            s.Onboarding.Step = step;
            s.Onboarding.Drill = step == OnboardingStep.Drill ? System.Math.Max(0, drill) : 0;
        }

        /// <summary>Marks the onboarding finished.</summary>
        public static void Finish(SaveData s)
        {
            if (s == null) return;
            s.Onboarding ??= new OnboardingSave();
            s.Onboarding.Step = OnboardingStep.Done;
            s.Onboarding.Drill = 0;
        }

        public static bool IsDone(SaveData s) => Next(s).Step == OnboardingStep.Done;

        /// <summary>The screen to resume <paramref name="s"/> at. With content, a drill names its stage, and a drill past
        /// the end of campaign.json Tutorial (or whose stage is gone) resumes at Camp's first visit instead.</summary>
        public static OnboardingResume Next(SaveData s, ContentDb c = null)
        {
            var o = s?.Onboarding;
            if (o == null) return new OnboardingResume(OnboardingStep.Done);
            switch (o.Step)
            {
                case OnboardingStep.Crawl:
                case OnboardingStep.HubIntro:
                    return new OnboardingResume(o.Step);
                case OnboardingStep.Drill:
                    int n = System.Math.Max(0, o.Drill);
                    if (c == null) return new OnboardingResume(OnboardingStep.Drill, n);
                    IReadOnlyList<string> tutorial = c.Campaign?.Tutorial;
                    string id = tutorial != null && n < tutorial.Count ? tutorial[n] : null;
                    return id != null && c.TryStage(id) != null
                        ? new OnboardingResume(OnboardingStep.Drill, n, id)
                        : new OnboardingResume(OnboardingStep.HubIntro);
                default:
                    return new OnboardingResume(OnboardingStep.Done);
            }
        }
    }
}
