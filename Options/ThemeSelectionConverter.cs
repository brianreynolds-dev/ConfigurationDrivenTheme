using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.Shell;

namespace ConfigurationThemeSwitcher.Options
{
	internal sealed class ThemeSelectionConverter : StringConverter
	{
		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
		{
			return true;
		}

		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var currentValue = getCurrentValue(context);
			return new StandardValuesCollection(ThemeDisplayNameResolver.GetThemeDisplayNames(currentValue, includeBlank: true).ToArray());
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			return value is string text ? text.Trim() : base.ConvertFrom(context, culture, value);
		}

		private static string getCurrentValue(ITypeDescriptorContext context)
		{
			if (context?.Instance == null || context.PropertyDescriptor == null)
			{
				return string.Empty;
			}

			return context.PropertyDescriptor.GetValue(context.Instance) as string ?? string.Empty;
		}
	}
}
