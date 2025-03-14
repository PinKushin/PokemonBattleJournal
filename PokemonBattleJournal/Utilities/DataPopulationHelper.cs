namespace PokemonBattleJournal.Utilities
{
    public static class DataPopulationHelper
    {
        //public static async Task<List<Archetype>> PopulateArchetypesAsync(SqliteConnectionFactory connection)
        //{
        //    List<Archetype> archetypes = await connection.GetArchetypesAsync();
        //    if (archetypes.Count == 0)
        //    {
        //        try
        //        {
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Regidrago", ImagePath = "regidrago.png" });
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Charizard", ImagePath = "charizard.png" });
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Klawf", ImagePath = "klawf.png" });
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Snorlax Stall", ImagePath = "snorlax.png" });
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Raging Bolt", ImagePath = "raging_bolt.png" });
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Gardevoir", ImagePath = "gardevoir.png" });
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Miraidon", ImagePath = "miraidon.png" });
        //            await connection.SaveArchetypeAsync(new Archetype() { Name = "Other", ImagePath = "ball_icon.png" });
        //        }
        //        catch (Exception ex)
        //        {
        //            ModalErrorHandler handle = new();
        //            handle.HandleError(ex);
        //        }
        //    }
        //    return archetypes;

        //}

        //public static async Task<IList<object>> PopulateTagsAsync(SqliteConnectionFactory connection)
        //{
        //    IList<object> tagCollection = await connection.GetTagsAsync();
        //    if (tagCollection.Count == 0)
        //    {
        //        try
        //        {
        //            await connection.SaveTagAsync(new Tags() { Name = "Early Start" });
        //            await connection.SaveTagAsync(new Tags() { Name = "Behind Early" });
        //            await connection.SaveTagAsync(new Tags() { Name = "Donked Rival" });
        //            await connection.SaveTagAsync(new Tags() { Name = "Got Donked" });
        //            await connection.SaveTagAsync(new Tags() { Name = "Lucky" });
        //            await connection.SaveTagAsync(new Tags() { Name = "Unlucky" });
        //            await connection.SaveTagAsync(new Tags() { Name = "Never Punished" });
        //            await connection.SaveTagAsync(new Tags() { Name = "Punished" });
        //        }
        //        catch (Exception ex)
        //        {
        //            ModalErrorHandler handle = new();
        //            handle.HandleError(ex);
        //        }
        //    }

        //    return tagCollection;
        //}
    }
}