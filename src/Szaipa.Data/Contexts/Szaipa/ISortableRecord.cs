namespace Szaipa.Data.Contexts.Szaipa;

/// <summary>
/// Shared shape of the hand-ordered, show/hide-able records behind the homepage static blocks and About page
/// (<see cref="OrgMember"/>, <see cref="SaleWork"/>, <see cref="Partner"/>). Lets one generic admin repository
/// provide the common create / update / delete / move-up-down / visibility-toggle flow.
/// </summary>
public interface ISortableRecord
{
    int Id { get; set; }

    /// <summary>Ascending display order (within the record's scope, e.g. an <see cref="OrgMember.GroupName"/>).</summary>
    int Sort { get; set; }

    /// <summary>False hides the row on the public site without deleting it.</summary>
    bool IsVisible { get; set; }
}
