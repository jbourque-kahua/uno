using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Private.Infrastructure;
using Uno.UI.RuntimeTests.Helpers;

#if HAS_UNO
using Uno.UI.Runtime.Skia;
#endif

namespace Uno.UI.RuntimeTests.Tests.Windows_UI_Xaml_Automation
{
	/// <summary>
	/// Runtime tests for generic ARIA attribute mapping that is not specific to a single
	/// control type. Covers the AutomationId-versus-accessible-name separation: an
	/// AutomationId is a stable test/automation identifier, not an accessible name, so it
	/// must never leak into the resolved label (and therefore never into aria-label).
	/// </summary>
	[TestClass]
	public class Given_AccessibleAria
	{
#if HAS_UNO
		/// <summary>
		/// T019/T020: A control with AutomationProperties.AutomationId set but no Name must NOT
		/// expose the AutomationId as its accessible name. aria-label is sourced only from the
		/// resolved name; the AutomationId travels separately (xamlautomationid). This asserts the
		/// AriaMapper side of that seam on Skia Desktop: ResolveLabel / GetAriaAttributes().Label
		/// must not equal the AutomationId.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		public async Task When_AutomationId_Without_Name_Then_AriaLabel_Is_Not_AutomationId()
		{
			const string automationId = "submit-button-automation-id";

			// A bare ContentControl (no Content, no Name) so the only candidate that could
			// wrongly become the label is the AutomationId itself.
			var control = new ContentControl();
			AutomationProperties.SetAutomationId(control, automationId);

			await UITestHelper.Load(control);

			var peer = FrameworkElementAutomationPeer.CreatePeerForElement(control);
			Assert.IsNotNull(peer, "Control should have an automation peer");

			var resolvedLabel = AriaMapper.ResolveLabel(peer);
			Assert.AreNotEqual(automationId, resolvedLabel, "ResolveLabel must not return the AutomationId as the accessible name");

			var attributes = AriaMapper.GetAriaAttributes(peer);
			Assert.AreNotEqual(automationId, attributes.Label, "aria-label must not be sourced from the AutomationId");
		}

		/// <summary>
		/// T019/T020: When both an AutomationId and a Name are present, the resolved label must be
		/// the Name (which maps to aria-label), never the AutomationId. Guards against a regression
		/// where the AutomationId would shadow or override the genuine accessible name.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		public async Task When_AutomationId_And_Name_Then_AriaLabel_Is_Name()
		{
			const string automationId = "save-button-automation-id";
			const string name = "Save document";

			var control = new ContentControl();
			AutomationProperties.SetAutomationId(control, automationId);
			AutomationProperties.SetName(control, name);

			await UITestHelper.Load(control);

			var peer = FrameworkElementAutomationPeer.CreatePeerForElement(control);
			Assert.IsNotNull(peer, "Control should have an automation peer");

			var attributes = AriaMapper.GetAriaAttributes(peer);
			Assert.AreEqual(name, attributes.Label, "aria-label must be the resolved Name");
			Assert.AreNotEqual(automationId, attributes.Label, "aria-label must not be the AutomationId even when a Name is also set");
		}

		/// <summary>
		/// Regression (FR-015): a TextBlock kept for an explicit LiveSetting/AutomationId must NOT be
		/// classified as a bare Text element (which emits only textContent and drops aria-live/xamlautomationid).
		/// It must map to Generic so the generic path re-emits those attributes. Guards the FR-015 over-capture
		/// regression where every kept TextBlock was routed to the Text element.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		public async Task When_LiveRegion_TextBlock_Then_SemanticType_Is_Generic()
		{
			var textBlock = new TextBlock { Text = "Ready" };
			AutomationProperties.SetLiveSetting(textBlock, AutomationLiveSetting.Polite);
			AutomationProperties.SetAutomationId(textBlock, "ButtonsStatus");

			await UITestHelper.Load(textBlock);

			var peer = FrameworkElementAutomationPeer.CreatePeerForElement(textBlock);
			Assert.IsNotNull(peer, "TextBlock should have an automation peer");

			Assert.AreEqual(
				SemanticElementType.Generic,
				AriaMapper.GetSemanticElementType(peer, textBlock),
				"A TextBlock with an explicit LiveSetting/AutomationId must take the generic path so aria-live and xamlautomationid are still emitted, not the bare Text path.");
		}

		/// <summary>
		/// FR-015 preserved: a plain body TextBlock with no explicit automation properties must still be
		/// classified as a Text element (emitted as a bare &lt;p&gt; carrying only its text). Guards against the
		/// regression fix over-triggering and turning ordinary body text into generic nodes.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		public async Task When_Plain_TextBlock_Then_SemanticType_Is_Text()
		{
			var textBlock = new TextBlock { Text = "Some body paragraph." };

			await UITestHelper.Load(textBlock);

			var peer = FrameworkElementAutomationPeer.CreatePeerForElement(textBlock);
			Assert.IsNotNull(peer, "TextBlock should have an automation peer");

			Assert.AreEqual(
				SemanticElementType.Text,
				AriaMapper.GetSemanticElementType(peer, textBlock),
				"A plain body TextBlock (no Name/Landmark/LiveSetting/AutomationId) must remain a bare Text element.");
		}
#endif

#if __SKIA__
		/// <summary>
		/// T019/T020 (WASM seam, handed off): On the WASM DOM path, a control with an AutomationId
		/// must surface it as the <c>xamlautomationid</c> attribute on its semantic element — NOT as
		/// aria-label. Validates the DOM side of the AutomationId/name split end to end in the
		/// browser. Depends on the WASM runtime addSemanticElement seam (separate xamlAutomationId
		/// arg); will pass once that seam lands.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_AutomationId_On_Wasm_Then_XamlAutomationId_Attribute_Is_Set()
		{
			const string automationId = "wasm-button-automation-id";

			var button = new Button { Content = "Click me" };
			AutomationProperties.SetAutomationId(button, automationId);

			await UITestHelper.Load(button);
			button.GetOrCreateAutomationPeer();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(button), timeoutMS: 5000, message: "Timed out waiting for the semantic element to be created.");
			await UITestHelper.WaitForIdle();

			Assert.AreEqual(automationId, GetSemanticAttribute(button, "xamlautomationid"), "Semantic element should expose the AutomationId as the xamlautomationid attribute.");

			var ariaLabel = GetSemanticAttribute(button, "aria-label");
			Assert.AreNotEqual(automationId, ariaLabel, "aria-label must not be sourced from the AutomationId on the DOM path.");
		}

		/// <summary>
		/// Regression (FR-015, WASM DOM seam): a TextBlock kept for an explicit LiveSetting + AutomationId must
		/// emit BOTH aria-live and xamlautomationid on its semantic element. FR-015 initially routed every
		/// TextBlock through the bare Text element (textContent only), dropping these; the fix routes
		/// explicit-property TextBlocks through the generic path. Fails before the fix, passes after.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_LiveRegion_TextBlock_On_Wasm_Then_AriaLive_And_XamlAutomationId_Are_Emitted()
		{
			const string automationId = "ButtonsStatus";

			var textBlock = new TextBlock { Text = "Ready" };
			AutomationProperties.SetLiveSetting(textBlock, AutomationLiveSetting.Polite);
			AutomationProperties.SetAutomationId(textBlock, automationId);

			await UITestHelper.Load(textBlock);
			textBlock.GetOrCreateAutomationPeer();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(textBlock), timeoutMS: 5000, message: "Timed out waiting for the semantic element to be created.");
			await UITestHelper.WaitForIdle();

			Assert.AreEqual("polite", GetSemanticAttribute(textBlock, "aria-live"), "A LiveSetting=Polite TextBlock must emit aria-live=polite (regressed by FR-015's bare Text path).");
			Assert.AreEqual(automationId, GetSemanticAttribute(textBlock, "xamlautomationid"), "An AutomationId on a kept TextBlock must be emitted as xamlautomationid (regressed by FR-015's bare Text path).");
		}

		/// <summary>
		/// T057 (FR-031, WASM): a virtualized container (NavigationView's MenuItemsHost ItemsRepeater)
		/// whose items are already realized when accessibility is enabled must still emit a semantic node
		/// per item. Before the build-time registration + backfill fix, CreateAOM pruned the repeater and
		/// never registered the virtualized region (OnChildAdded is suppressed during the build), so the
		/// whole nav was absent from the AT tree. This loads the NavigationView, THEN enables accessibility
		/// (the broken flow), and asserts each destination emits. Fails before the fix, passes after.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_NavigationView_Items_Realized_Before_Enable_Then_Each_Emits_Semantic_Node()
		{
			var home = new NavigationViewItem { Content = "Home" };
			var settings = new NavigationViewItem { Content = "Settings" };
			var nav = new NavigationView
			{
				PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
				IsPaneOpen = true,
				IsSettingsVisible = false,
				IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
				Width = 400,
				Height = 400,
			};
			nav.MenuItems.Add(home);
			nav.MenuItems.Add(settings);

			await UITestHelper.Load(nav);
			await UITestHelper.WaitForIdle();

			// Enable accessibility AFTER the items are realized — the flow that was broken.
			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(home), timeoutMS: 5000,
				message: "Timed out waiting for the NavigationView item semantic node (T057 backfill).");
			await UITestHelper.WaitForIdle();

			Assert.IsTrue(SemanticElementExists(home), "NavigationView item 'Home' must emit a semantic node when accessibility is enabled after load.");
			Assert.IsTrue(SemanticElementExists(settings), "NavigationView item 'Settings' must emit a semantic node when accessibility is enabled after load.");
		}

		/// <summary>
		/// T058 (FR-032, WASM): a Collapsed element must NOT be emitted to the AT tree (WinUI parity —
		/// Collapsed is absent from UIA), while a Visible sibling still emits. Guards the visibility-prune
		/// in the semantic-tree walk; the positive guard ensures the prune is not vacuously over-broad.
		/// Uses a typed Button (the SemanticElementFactory path that never threaded visibility).
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_Element_Collapsed_Then_No_Semantic_Node_While_Visible_Sibling_Emits()
		{
			var hidden = new Button { Content = "HiddenBtn", Visibility = Visibility.Collapsed };
			var visible = new Button { Content = "VisibleBtn" };
			var panel = new StackPanel();
			panel.Children.Add(hidden);
			panel.Children.Add(visible);

			await UITestHelper.Load(panel);
			await UITestHelper.WaitForIdle();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(visible), timeoutMS: 5000,
				message: "Timed out waiting for the visible button's semantic node.");
			await UITestHelper.WaitForIdle();

			Assert.IsTrue(SemanticElementExists(visible), "A visible Button must emit a semantic node.");
			Assert.IsFalse(SemanticElementExists(hidden), "A Collapsed Button must NOT emit a semantic node (FR-032/T058).");
		}

		/// <summary>
		/// T057 follow-up (FR-031, WASM): the virtualized backfill/handlers must NOT emit decorative
		/// AccessibilityView=Raw repeater children. A NavigationViewItemSeparator realized in the menu
		/// ItemsRepeater must produce NO semantic node, while a real item still does. Guards the
		/// IsSemanticElement gate added to EmitRealizedItem. Fails before the gate, passes after.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_NavigationView_Has_Raw_Separator_Then_It_Is_Not_Emitted()
		{
			var item = new NavigationViewItem { Content = "Home" };
			var separator = new NavigationViewItemSeparator();
			var nav = new NavigationView
			{
				PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
				IsPaneOpen = true,
				IsSettingsVisible = false,
				IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
				Width = 400,
				Height = 400,
			};
			nav.MenuItems.Add(item);
			nav.MenuItems.Add(separator);

			await UITestHelper.Load(nav);
			await UITestHelper.WaitForIdle();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(item), timeoutMS: 5000,
				message: "Timed out waiting for the NavigationView item semantic node.");
			await UITestHelper.WaitForIdle();

			Assert.IsTrue(SemanticElementExists(item), "A real NavigationView item must emit a semantic node.");
			Assert.IsFalse(SemanticElementExists(separator), "A decorative NavigationViewItemSeparator (AccessibilityView=Raw) must NOT be emitted as a semantic node (FR-031).");
		}

		/// <summary>
		/// T058 lazy re-emit (FR-032, WASM): an element Collapsed at AOM-build time is pruned (no node),
		/// but when it later flips Visibility=Collapsed->Visible it MUST be re-emitted — no other post-build
		/// path creates a node and there is no show-counterpart to HideSemanticElement. A visible sibling
		/// confirms the build completed before asserting the collapsed one is absent. Fails before the lazy
		/// re-emit (element stays unexposed forever), passes after.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_Collapsed_Element_Becomes_Visible_Then_Semantic_Node_Is_Emitted()
		{
			var visibleSibling = new Button { Content = "VisibleSibling" };
			var toggling = new Button { Content = "TogglingBtn", Visibility = Visibility.Collapsed };
			var panel = new StackPanel();
			panel.Children.Add(visibleSibling);
			panel.Children.Add(toggling);

			await UITestHelper.Load(panel);
			await UITestHelper.WaitForIdle();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(visibleSibling), timeoutMS: 5000,
				message: "Timed out waiting for the visible sibling's semantic node (build completion).");
			await UITestHelper.WaitForIdle();

			// Collapsed at build time => pruned (T058). The sibling above proves the AOM build finished.
			Assert.IsFalse(SemanticElementExists(toggling), "A Collapsed Button must not be emitted while collapsed.");

			// Flip to Visible => must be re-emitted by the lazy re-emit path in OnSizeOrOffsetChanged.
			toggling.Visibility = Visibility.Visible;
			await UITestHelper.WaitFor(() => SemanticElementExists(toggling), timeoutMS: 5000,
				message: "Timed out waiting for the re-emitted semantic node after Collapsed->Visible.");
			await UITestHelper.WaitForIdle();

			Assert.IsTrue(SemanticElementExists(toggling), "A Button that flips Collapsed->Visible must be re-emitted to the AT tree (T058 lazy re-emit).");
		}

		/// <summary>
		/// Focus-sync (FR-031, WASM): when XAML focus moves to a NavigationViewItem (a virtualized-container
		/// item emitted via the region path), DOM/AT focus must land on the ITEM's own semantic node, not its
		/// container ancestor. Before the fix, ResolveToSemanticHandle missed the item (tracked in the region,
		/// not _semanticParentMap) and walked up, so document.activeElement was the container. Fails before,
		/// passes after.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_NavigationViewItem_Focused_Then_Its_Own_Semantic_Node_Receives_DOM_Focus()
		{
			var home = new NavigationViewItem { Content = "Home" };
			var settings = new NavigationViewItem { Content = "Settings" };
			var nav = new NavigationView
			{
				PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
				IsPaneOpen = true,
				IsSettingsVisible = false,
				IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
				Width = 400,
				Height = 400,
			};
			nav.MenuItems.Add(home);
			nav.MenuItems.Add(settings);

			await UITestHelper.Load(nav);
			await UITestHelper.WaitForIdle();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(settings), timeoutMS: 5000,
				message: "Timed out waiting for the NavigationView item semantic node.");

			// Move XAML focus to the second destination.
			settings.Focus(FocusState.Keyboard);
			await UITestHelper.WaitForIdle();

			var expectedId = GetSemanticElementId(settings);

			// Root cause: DOM/AT focus must be on the item's OWN node, not the container ancestor.
			await UITestHelper.WaitFor(
				() => InvokeBrowserJs("(function(){return document.activeElement ? document.activeElement.id : '';})()") == expectedId,
				timeoutMS: 5000,
				message: "DOM focus did not move to the NavigationViewItem's own semantic node (focus-sync resolution gap).");

			var activeId = InvokeBrowserJs("(function(){return document.activeElement ? document.activeElement.id : '';})()");
			Assert.AreEqual(expectedId, activeId,
				"A focused NavigationViewItem's own DOM node must be document.activeElement, not its container ancestor.");
		}

		/// <summary>
		/// FR-031 (WASM): a decorative AccessibilityView=Raw ItemsRepeater (e.g. RadioButtons' InnerRepeater)
		/// must NOT be registered/emitted as a virtualized listbox region — doing so exposes a phantom
		/// listbox as clutter. The walkers instead recurse into it so non-decorative items still emit. A
		/// visible sibling confirms the build completed. Fails before the container Raw-gate, passes after.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_Repeater_Is_AccessibilityView_Raw_Then_Not_Emitted_As_Listbox()
		{
			var sibling = new Button { Content = "Sibling" };
			var repeater = new ItemsRepeater { ItemsSource = new[] { "Alpha", "Beta" } };
			AutomationProperties.SetAccessibilityView(repeater, AccessibilityView.Raw);
			var panel = new StackPanel();
			panel.Children.Add(sibling);
			panel.Children.Add(repeater);

			await UITestHelper.Load(panel);
			await UITestHelper.WaitForIdle();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(sibling), timeoutMS: 5000,
				message: "Timed out waiting for the visible sibling's semantic node (build completion).");
			await UITestHelper.WaitForIdle();

			Assert.IsTrue(SemanticElementExists(sibling), "A visible Button sibling must emit a semantic node.");
			Assert.IsFalse(SemanticElementExists(repeater),
				"A decorative AccessibilityView=Raw ItemsRepeater must not be emitted as a virtualized listbox (FR-031).");
		}

		/// <summary>
		/// FR-014: main/navigation/search are top-level landmarks identified by role alone — an unnamed
		/// Main must keep role=main. Only region/form (incl. Custom→region) require a name; an unnamed
		/// region must NOT emit a role. Guards against over-gating every landmark on a name.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_Unnamed_Main_Landmark_Then_Role_Kept_But_Unnamed_Region_Dropped()
		{
			var main = new Border();
			AutomationProperties.SetLandmarkType(main, AutomationLandmarkType.Main);
			var unnamedRegion = new Border();
			AutomationProperties.SetLandmarkType(unnamedRegion, AutomationLandmarkType.Custom);
			var panel = new StackPanel();
			panel.Children.Add(main);
			panel.Children.Add(unnamedRegion);

			await UITestHelper.Load(panel);
			await UITestHelper.WaitForIdle();
			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(main), timeoutMS: 5000,
				message: "Timed out waiting for the Main landmark semantic node.");
			await UITestHelper.WaitForIdle();

			Assert.AreEqual("main", GetSemanticAttribute(main, "role"),
				"An unnamed Main landmark must keep role=main (top-level landmark, no name required).");
			Assert.AreEqual(string.Empty, GetSemanticAttribute(unnamedRegion, "role"),
				"An unnamed Custom (region) landmark must NOT emit a role (region requires a name).");
		}

		/// <summary>
		/// T021 (FR-019, WASM): when AutomationProperties.LabeledBy points to an element that has its own
		/// semantic node, the labelled control must emit aria-labelledby as an IDREF to that labeller's
		/// uno-semantics-{handle} node — distinct from aria-label. The labeller (a Named TextBlock) is
		/// placed first so its semantic node already exists when the labelled Button is built, so the
		/// creation-time IDREF emits deterministically. Fails before T021 (LabelledBy was never populated),
		/// passes after.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_LabeledBy_Semantic_Element_Then_AriaLabelledBy_References_Labeller()
		{
			// Named TextBlock => kept as a semantic node (IsSemanticElement true). Placed first so its
			// node exists before the labelled Button is built.
			var labeller = new TextBlock { Text = "Email address" };
			AutomationProperties.SetName(labeller, "Email address");

			var labelled = new Button { Content = "Edit" };
			AutomationProperties.SetLabeledBy(labelled, labeller);

			var panel = new StackPanel();
			panel.Children.Add(labeller);
			panel.Children.Add(labelled);

			await UITestHelper.Load(panel);
			labeller.GetOrCreateAutomationPeer();
			labelled.GetOrCreateAutomationPeer();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(labeller) && SemanticElementExists(labelled), timeoutMS: 5000,
				message: "Timed out waiting for both the labeller and labelled semantic nodes.");
			await UITestHelper.WaitForIdle();

			var expectedIdRef = GetSemanticElementId(labeller);
			Assert.AreEqual(expectedIdRef, GetSemanticAttribute(labelled, "aria-labelledby"),
				"aria-labelledby must reference the labeller's uno-semantics-{handle} node.");
		}

		/// <summary>
		/// T021 (FR-019/FR-022, WASM): aria-labelledby must NOT be a dangling IDREF. When LabeledBy points
		/// to an element that has NO semantic node (an AccessibilityView=Raw Border is never emitted),
		/// the labelled control must not carry an aria-labelledby attribute at all. Guards the
		/// HasSemanticElement gate.
		/// </summary>
		[TestMethod]
		[RunsOnUIThread]
		[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
		public async Task When_LabeledBy_NonSemantic_Element_Then_No_Dangling_AriaLabelledBy()
		{
			// AccessibilityView=Raw => excluded from the AT tree (IsSemanticElement false), so it has no
			// uno-semantics node to reference.
			var nonSemanticLabeller = new Border();
			AutomationProperties.SetAccessibilityView(nonSemanticLabeller, AccessibilityView.Raw);

			var labelled = new Button { Content = "Edit" };
			AutomationProperties.SetLabeledBy(labelled, nonSemanticLabeller);

			var panel = new StackPanel();
			panel.Children.Add(nonSemanticLabeller);
			panel.Children.Add(labelled);

			await UITestHelper.Load(panel);
			labelled.GetOrCreateAutomationPeer();

			EnableAccessibilityThroughDom();
			await UITestHelper.WaitFor(() => SemanticElementExists(labelled), timeoutMS: 5000,
				message: "Timed out waiting for the labelled semantic node.");
			await UITestHelper.WaitForIdle();

			Assert.IsFalse(SemanticElementExists(nonSemanticLabeller),
				"An AccessibilityView=Raw Border must not have a semantic node (test precondition).");
			Assert.AreEqual(string.Empty, GetSemanticAttribute(labelled, "aria-labelledby"),
				"aria-labelledby must not be emitted when the labeller has no semantic node (no dangling IDREF).");
		}

		private static void EnableAccessibilityThroughDom()
		{
			InvokeBrowserJs("(function(){const button = document.getElementById('uno-enable-accessibility'); if (button) { button.click(); } return 'ok';})()");
		}

		// Targets the exact semantic element for a given element via its visual handle, mirroring the
		// id scheme used by the WASM runtime (uno-semantics-{handle}).
		private static string GetSemanticElementId(UIElement element)
			=> $"uno-semantics-{((long)element.Visual.Handle)}";

		private static bool SemanticElementExists(UIElement element)
			=> InvokeBrowserJs($"(function(){{return document.getElementById('{GetSemanticElementId(element)}') ? '1' : '0';}})()") == "1";

		private static string GetSemanticAttribute(UIElement element, string attribute)
			=> InvokeBrowserJs($"(function(){{const e = document.getElementById('{GetSemanticElementId(element)}'); return e ? (e.getAttribute('{attribute}') ?? '') : '';}})()");

		private static string InvokeBrowserJs(string javascript)
		{
			var runtimeType = Type.GetType("Uno.Foundation.WebAssemblyRuntime, Uno.Foundation.Runtime.WebAssembly", throwOnError: false);
			Assert.IsNotNull(runtimeType, "Unable to locate Uno.Foundation.WebAssemblyRuntime at runtime.");

			var invokeJs = runtimeType.GetMethod("InvokeJS", new[] { typeof(string) });
			Assert.IsNotNull(invokeJs, "Unable to locate Uno.Foundation.WebAssemblyRuntime.InvokeJS(string).");

			return invokeJs.Invoke(obj: null, parameters: new object[] { javascript }) as string ?? string.Empty;
		}
#endif
	}
}
