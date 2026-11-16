using FluentAssertions;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Tests;

// 2026-11-12 family room door: each name locked to the phone that took it first; the admin approves a new phone.
public class DoorTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
    private static Room Family() => RoomRules.New("Skejby", null, Now, "Martin, Vibz, Julius, Eigil, Bertil");

    [Test]
    public void Only_family_rooms_have_a_locked_door()
    {
        DoorRules.Locked(Family()).Should().BeTrue();
        DoorRules.Locked(RoomRules.New("Kunde", null, Now)).Should().BeFalse();
        DoorRules.Locked(RoomRules.New("Hold 36", null, Now, "A, B", group: true)).Should().BeFalse();
    }

    [Test]
    public void The_first_phone_takes_the_name_and_another_phone_waits()
    {
        var (r, res) = DoorRules.Knock(Family(), "vibz", "PHONE1", "iphone", Now);
        res.Should().Be(DoorResult.In);
        DoorRules.NameOf(r, "PHONE1").Should().Be("Vibz");

        var (r2, res2) = DoorRules.Knock(r, "Vibz", "STRANGER", "computer", Now);
        res2.Should().Be(DoorResult.Waiting);
        DoorRules.NameOf(r2, "STRANGER").Should().BeNull();
        r2.Pending.Should().ContainSingle(p => p.Name == "Vibz" && p.Kind == "computer");

        DoorRules.Knock(r2, "Vibz", "PHONE1", "iphone", Now).Result.Should().Be(DoorResult.In);
    }

    [Test]
    public void A_name_not_on_the_list_never_gets_in()
    {
        DoorRules.Knock(Family(), "Henno", "X", "computer", Now).Result.Should().Be(DoorResult.NotOnList);
    }

    [Test]
    public void One_phone_holds_one_name()
    {
        var (r, _) = DoorRules.Knock(Family(), "Julius", "PHONE1", "android", Now);
        DoorRules.Knock(r, "Eigil", "PHONE1", "android", Now).Result.Should().Be(DoorResult.Waiting);
        DoorRules.NameOf(r, "PHONE1").Should().Be("Julius");
    }

    [Test]
    public void The_admin_approves_a_new_phone_or_frees_the_name()
    {
        var (r, _) = DoorRules.Knock(Family(), "Vibz", "PHONE1", "iphone", Now);
        (r, _) = DoorRules.Knock(r, "Vibz", "HOMESCREEN", "iphone", Now);
        var approved = DoorRules.Approve(r, r.Pending![0].Id)!;
        DoorRules.NameOf(approved, "HOMESCREEN").Should().Be("Vibz");
        approved.Pending.Should().BeEmpty();

        var freed = DoorRules.Release(approved, "vibz");
        DoorRules.NameOf(freed, "PHONE1").Should().BeNull();
        DoorRules.Knock(freed, "Vibz", "NEWPHONE", "iphone", Now).Result.Should().Be(DoorResult.In);
    }

    [Test]
    public void A_turned_away_phone_is_gone_from_the_list()
    {
        var (r, _) = DoorRules.Knock(Family(), "Vibz", "PHONE1", "iphone", Now);
        (r, _) = DoorRules.Knock(r, "Vibz", "STRANGER", "computer", Now);
        DoorRules.Reject(r, r.Pending![0].Id).Pending.Should().BeEmpty();
    }
}
