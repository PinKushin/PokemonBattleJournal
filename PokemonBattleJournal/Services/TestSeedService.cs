namespace PokemonBattleJournal.Services;

#if DEBUG

using PokemonBattleJournal.Models;

public class TestSeedService(TrainerOperations trainerOps, MatchOperations matchOps)
{
    public async Task<(int trainerId, int matchCount)> SeedAsync(
        string trainerName = "TestTrainer",
        int matchCount = 3,
        string matchResult = "Win")
    {
        var trainer = new Trainer { Name = trainerName };
        int trainerId = await trainerOps.InsertTrainerAsync(trainer);

        for (int i = 0; i < matchCount; i++)
        {
            var match = new Match
            {
                TrainerId = trainerId,
                Result = matchResult,
                PlayerSelectedId = 1,
                RivalSelectedId = 1,
                UserNote = $"TestSeed-{i + 1}"
            };
            await matchOps.InsertMatchAsync(match);
        }

        return (trainerId, matchCount);
    }
}

#endif
