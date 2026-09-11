using System.Text;
using System.Linq;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Window_OutpostEventDialog : Window
	{
		private readonly OutpostEventInstance instance;

		private readonly Outpost outpost;

		private Vector2 scrollPosition;

		public override Vector2 InitialSize => new Vector2(600f, 500f);

		public Window_OutpostEventDialog(Outpost outpost, OutpostEventInstance instance)
		{
			this.outpost = outpost;
			this.instance = instance;
			doCloseX = true;
			closeOnClickedOutside = true;
			absorbInputAroundWindow = true;
		}

		public override void DoWindowContents(Rect inRect)
		{
			if (instance?.def == null)
			{
				Close();
				return;
			}
			Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), instance.def.LabelCap);
			Widgets.Label(new Rect(0f, 36f, inRect.width, 24f), instance.def.description ?? string.Empty);
			Widgets.Label(new Rect(0f, 66f, inRect.width, 24f), "DreamsOutposts.Remaining".Translate(OutpostEventUtility.RemainingTimeLabel(instance)));
			OutpostEventOption defaultOption = instance.def.options?.FirstOrDefault((OutpostEventOption option) => option != null && option.id == instance.def.defaultOptionId);
			if (defaultOption != null)
			{
				Widgets.Label(new Rect(0f, 90f, inRect.width, 24f), "DreamsOutposts.OnTimeout".Translate(defaultOption.label ?? defaultOption.id ?? "DreamsOutposts.UnknownOption".Translate().ToString()));
				if (defaultOption.effects != null && defaultOption.effects.Count > 0)
				{
					StringBuilder preview = new StringBuilder("DreamsOutposts.Result".Translate() + " ");
					for (int i = 0; i < defaultOption.effects.Count; i++)
					{
						if (defaultOption.effects[i] != null)
						{
							if (preview.Length > 8)
							{
								preview.Append(", ");
							}
							preview.Append(defaultOption.effects[i].GetPreview(new OutpostEventContext { outpost = outpost, instance = instance }));
						}
					}
					Widgets.Label(new Rect(0f, 114f, inRect.width, 24f), preview.ToString());
				}
			}
			Rect optionsRect = new Rect(0f, 140f, inRect.width, Mathf.Max(inRect.height - 140f, 0f));
			float contentHeight = CalculateOptionsHeight(Mathf.Max(optionsRect.width - 16f, 0f));
			Rect optionsViewRect = new Rect(0f, 0f, Mathf.Max(optionsRect.width - 16f, 0f), Mathf.Max(optionsRect.height, contentHeight));
			Widgets.BeginScrollView(optionsRect, ref scrollPosition, optionsViewRect, true);
			float y = 0f;
			Widgets.Label(new Rect(0f, y, optionsViewRect.width, 24f), "DreamsOutposts.Options".Translate());
			y += 28f;
			if (instance.def.options == null)
			{
				Widgets.EndScrollView();
				return;
			}
			for (int i = 0; i < instance.def.options.Count; i++)
			{
				OutpostEventOption option = instance.def.options[i];
				if (option == null)
				{
					continue;
				}
				Rect buttonRect = new Rect(0f, y, optionsViewRect.width, 30f);
				OutpostEventContext context = new OutpostEventContext
				{
					outpost = outpost,
					instance = instance
				};
				bool requirementsMet = OutpostEventUtility.CheckRequirements(option, context, out var failureReason);
				bool canSelect = option.playerSelectable && requirementsMet;
				bool previousEnabled = GUI.enabled;
				GUI.enabled = canSelect;
				if (Widgets.ButtonText(buttonRect, option.label ?? option.id ?? "Unknown option"))
				{
					OutpostEventUtility.ResolveByPlayer(outpost, instance, option);
					Widgets.EndScrollView();
					Close();
					return;
				}
				GUI.enabled = previousEnabled;
				float optionY = buttonRect.yMax + 4f;
				StringBuilder text = new StringBuilder(option.description ?? string.Empty);
				if (option.effects != null && option.effects.Count > 0)
				{
					text.AppendLine();
					text.AppendLine("DreamsOutposts.Result".Translate());
					for (int j = 0; j < option.effects.Count; j++)
					{
						if (option.effects[j] != null)
						{
							text.AppendLine(option.effects[j].GetPreview(null));
						}
					}
				}
				string optionText = text.ToString();
				if (!string.IsNullOrEmpty(optionText))
				{
					float textHeight = Text.CalcHeight(optionText, optionsViewRect.width);
					Widgets.Label(new Rect(0f, optionY, optionsViewRect.width, textHeight), optionText);
					optionY += textHeight + 4f;
				}
				if (!requirementsMet && !string.IsNullOrEmpty(failureReason))
				{
					float failureHeight = Text.CalcHeight(failureReason, optionsViewRect.width);
					Widgets.Label(new Rect(0f, optionY, optionsViewRect.width, failureHeight), failureReason);
					optionY += failureHeight + 4f;
				}
				y = optionY + 8f;
			}
			Widgets.EndScrollView();
		}

		private float CalculateOptionsHeight(float width)
		{
			float height = 28f;
			if (instance.def.options == null)
			{
				return height;
			}
			OutpostEventContext context = new OutpostEventContext
			{
				outpost = outpost,
				instance = instance
			};
			for (int i = 0; i < instance.def.options.Count; i++)
			{
				OutpostEventOption option = instance.def.options[i];
				if (option == null)
				{
					continue;
				}
				float optionHeight = 34f;
				StringBuilder text = new StringBuilder(option.description ?? string.Empty);
				if (option.effects != null && option.effects.Count > 0)
				{
					text.AppendLine();
					text.AppendLine("DreamsOutposts.Result".Translate());
					for (int j = 0; j < option.effects.Count; j++)
					{
						if (option.effects[j] != null)
						{
							text.AppendLine(option.effects[j].GetPreview(context));
						}
					}
				}
				if (text.Length > 0)
				{
					optionHeight += Text.CalcHeight(text.ToString(), width) + 4f;
				}
				if (!OutpostEventUtility.CheckRequirements(option, context, out var failureReason) && !string.IsNullOrEmpty(failureReason))
				{
					optionHeight += Text.CalcHeight(failureReason, width) + 4f;
				}
				height += optionHeight + 8f;
			}
			return height;
		}
	}
}
