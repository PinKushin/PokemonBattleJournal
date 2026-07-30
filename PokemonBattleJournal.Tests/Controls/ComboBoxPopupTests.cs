using PokemonBattleJournal.Controls;

namespace PokemonBattleJournal.Tests.Controls;

public class ComboBoxPopupTests
{
    private record Item(string Name, string Icon);

    private static readonly List<object> Items =
    [
        new Item("Dragapult ex", "https://example.com/dragapult.png"),
        new Item("N's Zoroark ex", "https://example.com/zoroark.png"),
        new Item("Alakazam Powerful Hand", "https://example.com/alakazam.png"),
        new Item("Hydrapple ex", "https://example.com/hydrapple.png"),
        new Item("Other", "ball_icon.png"),
    ];

    [Test]
    public void FilterItems_EmptyQuery_ReturnsAllItems()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems(Items, string.Empty, nameof(Item.Name));
        result.Count().ShouldBe(Items.Count);
    }

    [Test]
    public void FilterItems_NullQuery_ReturnsAllItems()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems(Items, null!, nameof(Item.Name));
        result.Count().ShouldBe(Items.Count);
    }

    [Test]
    public void FilterItems_ExactMatch_ReturnsSingleItem()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems(Items, "Dragapult ex", nameof(Item.Name));
        result.ShouldHaveSingleItem();
        ((Item)result.First()).Name.ShouldBe("Dragapult ex");
    }

    [Test]
    public void FilterItems_PartialMatch_ReturnsMatchingItems()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems(Items, "ex", nameof(Item.Name));
        result.Count().ShouldBe(3); // Dragapult ex, N's Zoroark ex, Hydrapple ex
    }

    [Test]
    public void FilterItems_CaseInsensitive_ReturnsMatch()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems(Items, "ALA", nameof(Item.Name));
        result.ShouldHaveSingleItem();
        ((Item)result.First()).Name.ShouldBe("Alakazam Powerful Hand");
    }

    [Test]
    public void FilterItems_NoMatch_ReturnsEmpty()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems(Items, "Pikachu", nameof(Item.Name));
        result.ShouldBeEmpty();
    }

    [Test]
    public void FilterItems_UnknownProperty_ReturnsEmpty()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems(Items, "ex", "NonExistentProp");
        result.ShouldBeEmpty();
    }

    [Test]
    public void FilterItems_EmptyList_ReturnsEmpty()
    {
        IEnumerable<object> result = ComboBoxPopup.FilterItems([], "ex", nameof(Item.Name));
        result.ShouldBeEmpty();
    }
}
