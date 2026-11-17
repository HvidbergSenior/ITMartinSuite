using ITMartinSnak.Domain;
using NUnit.Framework;

namespace ITMartinSnak.Tests;

// 2026-11-17 user: "that should be the template... only chat room".
public class KunChatTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    [Test] public void A_plain_new_room_is_only_the_chat() => Assert.True(RoomRules.New("PerMartin", null, Now).KunChat);
    [Test] public void A_room_with_names_keeps_tasks_and_files() => Assert.False(RoomRules.New("Sonja", null, Now, "Vibz, Bent").KunChat);
    [Test] public void A_group_keeps_its_tabs() => Assert.False(RoomRules.New("Hold 37", null, Now, group: true).KunChat);
}
