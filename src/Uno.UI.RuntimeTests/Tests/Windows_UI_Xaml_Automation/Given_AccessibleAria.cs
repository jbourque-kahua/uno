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
