#if VISTA
using Pinwheel.VistaEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// First-run onboarding. The welcome leads into a short, visual carousel that introduces Vista's
    /// multi-biome workflow. The carousel stays inside this page so its arrows do not affect the wizard
    /// navigation stack.
    /// </summary>
    public class GetStartedPage : WizardPage
    {
        private const string USS_PATH = "Vista/USS/GetStarted";

        private enum NavDirection
        {
            Previous,
            Next
        }

        private readonly struct Step
        {
            public readonly string heading;
            public readonly string description;
            public readonly string imagePath;
            public readonly string placeholder;

            public Step(string heading, string description, string imagePath, string placeholder)
            {
                this.heading = heading;
                this.description = description;
                this.imagePath = imagePath;
                this.placeholder = placeholder;
            }
        }

        private static readonly Step[] s_steps =
        {
            new Step(
                "Create Your Biomes",
                "Start each biome from a template or build one from scratch.",
                "Vista/Textures/Onboarding/onboard-create-biomes",
                "16:9 image placeholder\nMultiple biome templates and a blank biome"),
            new Step(
                "Shape Them with Graphs",
                "Edit each biome's graph to create its terrain, textures, vegetation, and details.",
                "Vista/Textures/Onboarding/onboard-edit-graph",
                "16:9 image placeholder\nMultiple biomes with their terrain graphs"),
            new Step(
                "Blend Biomes into Your World",
                "Place, overlap, and blend biomes to create varied landscapes with natural looking transitions.",
                "Vista/Textures/Onboarding/onboard-blend-biomes",
                "16:9 image placeholder\nA scene composed from multiple overlapping biomes"),
        };

        private static StyleSheet s_styleSheet;

        private VisualElement m_content;
        private bool m_hasStarted;
        private int m_stepIndex;

        public override string title => "Get Started";

        protected override void OnBuildBody(VisualElement content)
        {
            if (s_styleSheet == null)
                s_styleSheet = Resources.Load<StyleSheet>(USS_PATH);
            if (s_styleSheet != null)
                content.styleSheets.Add(s_styleSheet);

            m_content = content;
            Render();
        }

        private void Render()
        {
            m_content.Clear();
            m_content.Add(m_hasStarted ? BuildCarousel() : BuildWelcome());
        }

        private VisualElement BuildWelcome()
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("get-started-welcome");

            Image logo = new Image
            {
                image = EditorCommon.LoadIcon("res:Vista/Textures/VistaIcon"),
                scaleMode = ScaleMode.ScaleToFit
            };
            logo.AddToClassList("get-started-welcome__logo");
            root.Add(logo);

            root.Add(VistaUI.Label("Welcome to Vista").Title());
            root.Add(VistaUI.Label(
                "Build rich procedural worlds by creating and blending multiple biomes.")
                .P1());

            VisualElement actions = new VisualElement();
            actions.AddToClassList("get-started-welcome__actions");
            actions.Add(VistaUI.Button("See How It Works", Start).Primary());
            actions.Add(VistaUI.Clickable("Skip", SkipToHome).Faded().WithClass("get-started-welcome__skip"));
            root.Add(actions);
            return root;
        }

        private VisualElement BuildCarousel()
        {
            Step step = s_steps[m_stepIndex];
            VisualElement root = new VisualElement();
            root.AddToClassList("get-started-carousel");

            VisualElement frame = new VisualElement();
            frame.AddToClassList("get-started-carousel__frame");

            ClickableElement previous = BuildNavButton(NavDirection.Previous);
            previous.style.visibility = m_stepIndex > 0 ? Visibility.Visible : Visibility.Hidden;
            frame.Add(previous);

            VisualElement page = new VisualElement();
            page.AddToClassList("get-started-page");
            page.Add(VistaUI.Label(step.heading).H1());
            page.Add(VistaUI.Label(step.description).P1()); 
            page.Add(BuildStepImage(step));

            VisualElement actionSlot = new VisualElement();
            actionSlot.AddToClassList("get-started-page__action");
            if (m_stepIndex == s_steps.Length - 1)
                actionSlot.Add(VistaUI.Button("Build Your World", FinishToCreateInScene).Primary());
            page.Add(actionSlot);

            page.Add(BuildPaginator());
            frame.Add(page);

            ClickableElement next = BuildNavButton(NavDirection.Next);
            next.style.visibility = m_stepIndex < s_steps.Length - 1 ? Visibility.Visible : Visibility.Hidden;
            frame.Add(next);

            root.Add(frame);
            return root;
        }

        private static VisualElement BuildStepImage(Step step)
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("get-started-page__image");

            Texture2D texture = string.IsNullOrEmpty(step.imagePath)
                ? null
                : Resources.Load<Texture2D>(step.imagePath);
            if (texture != null)
            {
                Image image = new Image
                {
                    image = texture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.AddToClassList("get-started-page__image-content");
                container.Add(image);
            }
            else
            {
                container.Add(VistaUI.Label(step.placeholder).P2().Faded());
            }

            container.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float height = evt.newRect.width * 9f / 16f;
                if (height > 0 && !Mathf.Approximately(evt.newRect.height, height))
                    container.style.height = height;
            });
            return container;
        }

        private ClickableElement BuildNavButton(NavDirection direction)
        {
            bool isNext = direction == NavDirection.Next;
            ClickableElement clickable = VistaUI.Clickable(string.Empty, isNext ? Next : Previous);
            clickable.AddToClassList("get-started-nav");
            clickable.tooltip = isNext ? "Next" : "Previous";

            Image icon = new Image
            {
                image = EditorGUIUtility.IconContent(isNext ? "tab_next" : "tab_prev").image,
                scaleMode = ScaleMode.ScaleToFit
            };
            icon.AddToClassList("get-started-nav__icon");
            clickable.Add(icon);
            return clickable;
        }

        private VisualElement BuildPaginator()
        {
            VisualElement paginator = new VisualElement();
            paginator.AddToClassList("get-started-paginator");
            paginator.tooltip = $"Step {m_stepIndex + 1} of {s_steps.Length}";

            for (int i = 0; i < s_steps.Length; ++i)
            {
                VisualElement dot = new VisualElement();
                dot.AddToClassList("get-started-paginator__dot");
                if (i == m_stepIndex)
                    dot.AddToClassList("get-started-paginator__dot--active");
                paginator.Add(dot);
            }
            return paginator;
        }

        private void Start()
        {
            OnboardingManager.MarkOnboardingShown();
            m_hasStarted = true;
            m_stepIndex = 0;
            Render();
        }

        private void Previous()
        {
            if (m_stepIndex <= 0)
                return;
            m_stepIndex--;
            Render();
        }

        private void Next()
        {
            if (m_stepIndex >= s_steps.Length - 1)
                return;
            m_stepIndex++;
            Render();
        }

        private void SkipToHome()
        {
            OnboardingManager.MarkOnboardingShown();
            host.ResetTo(new HomePage());
        }

        private void FinishToCreateInScene()
        {
            OnboardingManager.MarkOnboardingShown();
            host.ResetTo(new CreateInScenePage());
        }
    }

    internal static class OnboardingVisualElementExtensions
    {
        public static T WithClass<T>(this T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            return element;
        }
    }
}
#endif
