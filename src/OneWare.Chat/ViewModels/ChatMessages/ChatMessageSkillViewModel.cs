using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Controls;

namespace OneWare.Chat.ViewModels.ChatMessages;

/// <summary>
/// Shows that the AI pulled a skill into its context. The instructions are collapsed by default:
/// they are written for the model, but being able to read them explains the AI's behaviour.
/// </summary>
public class ChatMessageSkillViewModel(string skillName, string content)
    : ObservableObject, IChatMessage, IEstimatedHeightItem
{
    [DataMember] public string SkillName { get; } = skillName;

    [DataMember] public string Content { get; } = content;

    /// <summary>Collapsed by default, so only the header contributes to the estimated height.</summary>
    public double EstimateHeight(double width) => 36;
}
