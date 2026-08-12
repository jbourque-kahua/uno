#nullable enable

using System.Threading.Tasks;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Private.Infrastructure;
using Uno.UI.RuntimeTests.Helpers;
using static Uno.UI.RuntimeTests.Tests.Windows_UI_Xaml_Automation.WasmSemanticDomHelper;

namespace Uno.UI.RuntimeTests.Tests.Windows_UI_Xaml_Automation;

[TestClass]
public class Given_AccessibleSplitButton
{
	[TestMethod]
	[RunsOnUIThread]
	[PlatformCondition(ConditionMode.Include, RuntimeTestPlatforms.SkiaWasm)]
	public async Task When_SplitButton_Is_Closed_Then_Dom_Exposes_Grouped_Primary_And_Menu_Actions()
	{
		var splitButton = new SplitButton
		{
			Content = "Export",
		};
		AutomationProperties.SetName(splitButton, "Export actions");
		splitButton.Flyout = new MenuFlyout();
		((MenuFlyout)splitButton.Flyout).Items.Add(new MenuFlyoutItem { Text = "Export as PDF" });

		await UITestHelper.Load(splitButton);
		splitButton.GetOrCreateAutomationPeer();

		EnableAccessibilityThroughDom();
		await UITestHelper.WaitFor(() => SemanticElementExists(splitButton), timeoutMS: 5000, message: "Timed out waiting for the SplitButton group semantic element.");
		await UITestHelper.WaitForIdle();

		Assert.AreEqual("group", GetSemanticAttribute(splitButton, "role"));
		Assert.AreEqual("Export actions", GetSemanticAttribute(splitButton, "aria-label"));
		Assert.AreEqual(string.Empty, GetSemanticAttribute(splitButton, "aria-expanded"));

		var semanticChildren = InvokeBrowserJs($"(function(){{const group = document.getElementById('{GetSemanticElementId(splitButton)}'); return Array.from(group.querySelectorAll(':scope > button')).map(button => [button.getAttribute('aria-label'), button.getAttribute('aria-haspopup'), button.getAttribute('aria-expanded')].join('|')).join(',');}})()");
		Assert.AreEqual("Export||,More options|menu|false", semanticChildren);

		splitButton.OpenFlyout();
		await UITestHelper.WaitFor(() => InvokeBrowserJs($"(function(){{const group = document.getElementById('{GetSemanticElementId(splitButton)}'); const trigger = group?.querySelector(':scope > button[aria-haspopup=menu]'); const menuId = trigger?.getAttribute('aria-controls'); return menuId && document.getElementById(menuId)?.getAttribute('role') === 'menu' ? '1' : '0';}})()") == "1", timeoutMS: 5000, message: "Timed out waiting for the SplitButton trigger to reference its open menu.");

		var expandedTrigger = InvokeBrowserJs($"(function(){{const group = document.getElementById('{GetSemanticElementId(splitButton)}'); const trigger = group?.querySelector(':scope > button[aria-haspopup=menu]'); return [trigger?.getAttribute('aria-expanded'), trigger?.getAttribute('aria-controls')].join('|');}})()");
		StringAssert.StartsWith(expandedTrigger, "true|uno-semantics-");
		Assert.AreEqual(string.Empty, GetSemanticAttribute(splitButton, "aria-expanded"));
	}
}
