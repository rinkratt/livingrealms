using LivingRealms.Domain.Entities;

namespace LivingRealms.Domain.Tests;

public sealed class EntityDefaultsTests
{
    [Fact]
    public void NewCreatureStartsAliveAtLevelOne()
    {
        var creature = new Creature { Name = "Gray-Ear" };

        Assert.Equal(1, creature.Level);
        Assert.Equal(CreatureStatus.Alive, creature.Status);
        Assert.NotEqual(Guid.Empty, creature.Id);
    }

    [Fact]
    public void NewScheduledEventStartsPendingWithEmptyJsonPayload()
    {
        var scheduledEvent = new ScheduledEvent
        {
            EventType = "FactionResourceUpdate",
            ScheduledAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(ScheduledEventStatus.Pending, scheduledEvent.Status);
        Assert.Equal("{}", scheduledEvent.PayloadJson);
        Assert.Equal(0, scheduledEvent.RetryCount);
    }

    [Fact]
    public void NewCharacterStartsAtLevelOneWithFullHealth()
    {
        var character = new Character
        {
            AccountId = Guid.NewGuid(),
            Name = "Alden",
            Archetype = CharacterArchetype.Vanguard
        };

        Assert.Equal(1, character.Level);
        Assert.Equal(100, character.Health);
        Assert.Equal(character.MaximumHealth, character.Health);
    }
}
