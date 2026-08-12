#nullable enable

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Uno.UI.Runtime.Skia;

internal static class SplitButtonSemanticParts
{
	internal static bool TryGetOwner(UIElement element, out SplitButton? owner, out bool isSecondary)
	{
		owner = null;
		isSecondary = false;
		if (element is not Button { Name: SplitButton.PrimaryButtonPartName or SplitButton.SecondaryButtonPartName } button)
		{
			return false;
		}

		for (var parent = button.GetParent() as UIElement; parent is not null; parent = parent.GetParent() as UIElement)
		{
			if (parent is SplitButton splitButton)
			{
				owner = splitButton;
				isSecondary = button.Name == SplitButton.SecondaryButtonPartName;
				return true;
			}
		}

		return false;
	}

	internal static bool IsTemplatePart(UIElement element, out bool isSecondary)
	{
		return TryGetOwner(element, out _, out isSecondary);
	}

	internal static bool IsSecondaryTemplatePart(UIElement element)
		=> IsTemplatePart(element, out var isSecondary) && isSecondary;
}
