#if DEBUG
namespace PokemonBattleJournal.DevSeed
{
    internal static class DebugDataSeeder
    {
        internal static async Task SeedAsync(ISqliteConnectionFactory factory, ILogger logger)
        {
            try
            {
                var trainers = await factory.Trainers.GetAllAsync();
                var existing = trainers.FirstOrDefault(t => t.Name == "UITestTrainer");
                if (existing != null)
                {
                    if (!existing.IsActive)
                        await factory.Trainers.SetActiveAsync(existing);
                    return;
                }

                await factory.Trainers.SaveAsync("UITestTrainer");
                Trainer? trainer = await factory.Trainers.GetByNameAsync("UITestTrainer");
                if (trainer == null) return;

                await factory.Trainers.SetActiveAsync(trainer);

                List<Archetype> allArchetypes = await factory.Archetypes.GetAllAsync();
                // "Other" is always seeded by ArchetypeOperations regardless of Limitless result.
                Archetype? other = allArchetypes.FirstOrDefault(a => a.Name == "Other");
                if (other == null)
                {
                    logger.LogError("DebugDataSeeder: 'Other' archetype missing after GetAllAsync — aborting");
                    return;
                }

                // Substring match so "Charizard ex" (live Limitless) and "Charizard" (offline default) both resolve.
                // Falls back to Other for any archetype not currently in meta.
                static Archetype? Find(List<Archetype> list, string keyword) =>
                    list.FirstOrDefault(a => a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                Archetype charizard  = Find(allArchetypes, "Charizard")  ?? other;
                Archetype regidrago  = Find(allArchetypes, "Regidrago")  ?? other;
                Archetype miraidon   = Find(allArchetypes, "Miraidon")   ?? other;
                Archetype ragingBolt = Find(allArchetypes, "Raging Bolt") ?? other;
                Archetype gardevoir  = Find(allArchetypes, "Gardevoir")  ?? other;
                Archetype klawf      = Find(allArchetypes, "Klawf")      ?? other;

                List<Tags> allTags = await factory.Tags.GetAllAsync();
                Tags? lucky       = allTags.FirstOrDefault(t => t.Name == "Lucky");
                Tags? unlucky     = allTags.FirstOrDefault(t => t.Name == "Unlucky");
                Tags? earlyStart  = allTags.FirstOrDefault(t => t.Name == "Early Start");
                Tags? behindEarly = allTags.FirstOrDefault(t => t.Name == "Behind Early");
                Tags? neverPunish = allTags.FirstOrDefault(t => t.Name == "Never Punished");
                Tags? punished    = allTags.FirstOrDefault(t => t.Name == "Punished");

                DateTime baseDate = DateTime.UtcNow.AddDays(-14);

                (Archetype p, Archetype a, MatchResult res, uint turn, int days, int mins, Tags? tag)[] bo1 =
                [
                    (other,      charizard,  MatchResult.Win,  1u,  0,  12, null),
                    (charizard,  regidrago,  MatchResult.Win,  2u,  1,  18, earlyStart),
                    (regidrago,  miraidon,   MatchResult.Loss, 1u,  2,  22, behindEarly),
                    (other,      regidrago,  MatchResult.Win,  2u,  3,  15, lucky),
                    (ragingBolt, charizard,  MatchResult.Loss, 1u,  4,  10, unlucky),
                    (charizard,  miraidon,   MatchResult.Win,  1u,  5,  20, null),
                    (gardevoir,  ragingBolt, MatchResult.Tie,  2u,  6,  30, null),
                    (miraidon,   other,      MatchResult.Win,  1u,  7,  11, neverPunish),
                    (klawf,      charizard,  MatchResult.Loss, 2u,  8,  25, punished),
                    (regidrago,  gardevoir,  MatchResult.Win,  1u,  9,  14, lucky),
                    (charizard,  klawf,      MatchResult.Loss, 2u, 10,  19, null),
                    (other,      miraidon,   MatchResult.Win,  1u, 11,  16, earlyStart),
                ];

                for (int i = 0; i < bo1.Length; i++)
                {
                    var (playing, against, result, turn, days, mins, tag) = bo1[i];
                    DateTime date = baseDate.AddDays(days).AddHours(i % 3 * 4);
                    await factory.Matches.SaveAsync(
                        new MatchEntry
                        {
                            TrainerId = trainer.Id,
                            PlayingId = playing.Id,
                            AgainstId = against.Id,
                            Result = result,
                            DatePlayed = date,
                            StartTime = date,
                            EndTime = date.AddMinutes(mins)
                        },
                        [new Game { Result = result, Turn = turn, Notes = $"Seed-BO1-{i + 1}", Tags = tag != null ? [tag] : null }]);
                }

                DateTime bo3Date1 = baseDate.AddDays(12);
                await factory.Matches.SaveAsync(
                    new MatchEntry
                    {
                        TrainerId = trainer.Id,
                        PlayingId = other.Id,
                        AgainstId = charizard.Id,
                        Result = MatchResult.Win,
                        DatePlayed = bo3Date1,
                        StartTime = bo3Date1,
                        EndTime = bo3Date1.AddMinutes(45)
                    },
                    [
                        new Game { Result = MatchResult.Win,  Turn = 1, Notes = "Seed-BO3a-G1", Tags = lucky != null ? [lucky] : null },
                        new Game { Result = MatchResult.Loss, Turn = 2, Notes = "Seed-BO3a-G2", Tags = behindEarly != null ? [behindEarly] : null },
                        new Game { Result = MatchResult.Win,  Turn = 1, Notes = "Seed-BO3a-G3" }
                    ]);

                // +1 day ensures this match always sorts FIRST in ReadJournal (newest-first).
                // Other test-run matches are created at DateTime.Now (<= today), so this never slips.
                DateTime bo3Date2 = DateTime.UtcNow.AddDays(1);
                await factory.Matches.SaveAsync(
                    new MatchEntry
                    {
                        TrainerId = trainer.Id,
                        PlayingId = regidrago.Id,
                        AgainstId = ragingBolt.Id,
                        Result = MatchResult.Loss,
                        DatePlayed = bo3Date2,
                        StartTime = bo3Date2,
                        EndTime = bo3Date2.AddMinutes(52)
                    },
                    [
                        new Game { Result = MatchResult.Loss, Turn = 2, Notes = "Seed-BO3b-G1", Tags = punished != null ? [punished] : null },
                        new Game { Result = MatchResult.Win,  Turn = 1, Notes = "Seed-BO3b-G2" },
                        new Game { Result = MatchResult.Loss, Turn = 2, Notes = "Seed-BO3b-G3", Tags = unlucky != null ? [unlucky] : null }
                    ]);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DebugDataSeeder failed");
            }
        }
    }
}
#endif
