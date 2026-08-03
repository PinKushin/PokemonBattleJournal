using System.Text.Json;
using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Services.Import;

namespace PokemonBattleJournal.Tests.Services
{
    public class TrainerHillImportServiceTests
    {
        // ---------------------------------------------------------------------------
        // BuildSlugLookup / LookupSlug
        // ---------------------------------------------------------------------------

        [Test]
        public void BuildSlugLookup_SimpleSlug_Matches()
        {
            Dictionary<string, string> lookup = TrainerHillImportService.BuildSlugLookup(
                [new MetaDeck("Dragapult Dusknoir", "")]);
            TrainerHillImportService.LookupSlug("dragapult-dusknoir", lookup).ShouldBe("Dragapult Dusknoir");
        }

        [Test]
        public void BuildSlugLookup_PossessiveApostropheRemoved_MatchesRocketSlug()
        {
            Dictionary<string, string> lookup = TrainerHillImportService.BuildSlugLookup(
                [new MetaDeck("Rocket's Honchkrow", "")]);
            TrainerHillImportService.LookupSlug("rockets-honchkrow", lookup).ShouldBe("Rocket's Honchkrow");
        }

        [Test]
        public void BuildSlugLookup_PossessiveSDropped_MatchesNSlug()
        {
            Dictionary<string, string> lookup = TrainerHillImportService.BuildSlugLookup(
                [new MetaDeck("N's Zoroark", "")]);
            TrainerHillImportService.LookupSlug("n-zoroark", lookup).ShouldBe("N's Zoroark");
        }

        [Test]
        public void BuildSlugLookup_LimitlessVersionQualifier_MatchesSlugWithoutVersion()
        {
            Dictionary<string, string> lookup = TrainerHillImportService.BuildSlugLookup(
                [new MetaDeck("Dragapult ex / Dusknoir", "")]);
            TrainerHillImportService.LookupSlug("dragapult-dusknoir", lookup).ShouldBe("Dragapult ex / Dusknoir");
        }

        [Test]
        public void BuildSlugLookup_UnknownSlug_ReturnsNull()
        {
            Dictionary<string, string> lookup = TrainerHillImportService.BuildSlugLookup(
                [new MetaDeck("Other", "")]);
            TrainerHillImportService.LookupSlug("unknown-deck", lookup).ShouldBeNull();
        }

        [Test]
        public void BuildSlugLookup_EmptyList_ReturnsEmptyLookup()
        {
            Dictionary<string, string> lookup = TrainerHillImportService.BuildSlugLookup([]);
            TrainerHillImportService.LookupSlug("anything", lookup).ShouldBeNull();
        }

        // ---------------------------------------------------------------------------
        // SlugToName
        // ---------------------------------------------------------------------------

        [Test]
        public void SlugToName_SingleWord_ReturnsTitleCase() =>
            TrainerHillImportService.SlugToName("other").ShouldBe("Other");

        [Test]
        public void SlugToName_CompoundSlug_ReturnsSpacedTitleCase() =>
            TrainerHillImportService.SlugToName("dragapult-dusknoir").ShouldBe("Dragapult Dusknoir");

        [Test]
        public void SlugToName_TwoWordSlug_ReturnsTitleCase() =>
            TrainerHillImportService.SlugToName("festival-lead").ShouldBe("Festival Lead");

        // ---------------------------------------------------------------------------
        // ParseResult
        // ---------------------------------------------------------------------------

        [Test]
        public void ParseResult_Win_ReturnsWin() =>
            TrainerHillImportService.ParseResult("Win").ShouldBe(MatchResult.Win);

        [Test]
        public void ParseResult_Loss_ReturnsLoss() =>
            TrainerHillImportService.ParseResult("Loss").ShouldBe(MatchResult.Loss);

        [Test]
        public void ParseResult_Tie_ReturnsTie() =>
            TrainerHillImportService.ParseResult("Tie").ShouldBe(MatchResult.Tie);

        [Test]
        public void ParseResult_Lowercase_ReturnsParsed() =>
            TrainerHillImportService.ParseResult("win").ShouldBe(MatchResult.Win);

        [Test]
        public void ParseResult_Unknown_ReturnsNull() =>
            TrainerHillImportService.ParseResult("Draw").ShouldBeNull();

        // ---------------------------------------------------------------------------
        // ParseTurn
        // ---------------------------------------------------------------------------

        [Test]
        public void ParseTurn_IntegerOne_ReturnsOne()
        {
            JsonElement el = JsonDocument.Parse("1").RootElement;
            TrainerHillImportService.ParseTurn(el).ShouldBe(1u);
        }

        [Test]
        public void ParseTurn_IntegerTwo_ReturnsTwo()
        {
            JsonElement el = JsonDocument.Parse("2").RootElement;
            TrainerHillImportService.ParseTurn(el).ShouldBe(2u);
        }

        [Test]
        public void ParseTurn_StringOne_ReturnsOne()
        {
            JsonElement el = JsonDocument.Parse("\"1\"").RootElement;
            TrainerHillImportService.ParseTurn(el).ShouldBe(1u);
        }

        [Test]
        public void ParseTurn_StringTwo_ReturnsTwo()
        {
            JsonElement el = JsonDocument.Parse("\"2\"").RootElement;
            TrainerHillImportService.ParseTurn(el).ShouldBe(2u);
        }

        [Test]
        public void ParseTurn_Null_DefaultsToOne()
        {
            JsonElement el = JsonDocument.Parse("null").RootElement;
            TrainerHillImportService.ParseTurn(el).ShouldBe(1u);
        }
    }
}
