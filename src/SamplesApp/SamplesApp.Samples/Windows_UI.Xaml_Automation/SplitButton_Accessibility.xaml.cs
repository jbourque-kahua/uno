#nullable enable

using Microsoft.UI.Xaml.Controls;
using Uno.UI.Samples.Controls;

namespace UITests.Shared.Windows_UI_Xaml_Automation;

[Sample(
	"Accessibility",
	"SplitButton",
	Description = "Native SplitButton sample for verifying the grouped primary action and menu-button accessibility tree.",
	IsManualTest = true)]
public sealed partial class SplitButton_Accessibility : Page
{
	public SplitButton_Accessibility()
	{
		this.InitializeComponent();
	}
}
