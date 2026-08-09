using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Helpers
{
    public static class SelectListExtensions
    {
        public static List<SelectListItem> ToSelectList<T>(
            this IEnumerable<T> items,
            Func<T, string> valueSelector,
            Func<T, string> textSelector,
            string? selectedValue = null,
            string? optionLabel = null)
        {
            var list = items.Select(item =>
            {
                var val = valueSelector(item);
                return new SelectListItem
                {
                    Value = val,
                    Text = textSelector(item),
                    Selected = selectedValue != null && val == selectedValue
                };
            }).ToList();

            if (!string.IsNullOrEmpty(optionLabel))
            {
                list.Insert(0, new SelectListItem { Value = "", Text = optionLabel });
            }

            return list;
        }

        public static List<SelectListItem> EnumToSelectList<TEnum>(TEnum? selectedValue = null)
            where TEnum : struct, Enum
        {
            return Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(e => new SelectListItem
                {
                    Value = e.ToString(),
                    Text = e.ToString(),
                    Selected = selectedValue.HasValue && EqualityComparer<TEnum>.Default.Equals(e, selectedValue.Value)
                })
                .ToList();
        }
    }
}
