using FluentAssertions;
using ITMartinKontrol.Domain;

namespace ITMartinKontrol.Tests;

public class OpeningHoursTests
{
    [TestCase("07-17", 7, 17)]
    [TestCase(" 8 - 16 ", 8, 16)]
    [TestCase("22-06", 22, 6)]
    [TestCase("06-24", 6, 0)]
    public void Parse(string text, int from, int to) => OpeningHours.Parse(text).Should().Be(new OpeningHours(from, to));

    [TestCase(null)]
    [TestCase("")]
    [TestCase("altid")]
    [TestCase("07-07")]
    [TestCase("25-03")]
    [TestCase("7")]
    public void Parse_rejects(string? text) => OpeningHours.Parse(text).Should().BeNull();

    [TestCase("06:59", false)]
    [TestCase("07:00", true)]
    [TestCase("16:59", true)]
    [TestCase("17:00", false)]
    public void Polstrer_7_to_17(string time, bool open) =>
        new OpeningHours(7, 17).IsOpenAt(TimeOnly.Parse(time)).Should().Be(open);

    [TestCase("23:00", true)]
    [TestCase("05:30", true)]
    [TestCase("12:00", false)]
    public void Over_midnight(string time, bool open) =>
        new OpeningHours(22, 6).IsOpenAt(TimeOnly.Parse(time)).Should().Be(open);

    [TestCase(false, true, false, HoursStep.Start)]    // 07:00, it is off -> start
    [TestCase(true, false, true, HoursStep.Stop)]      // 17:00, it runs -> stop
    [TestCase(true, true, false, HoursStep.None)]      // stopped by hand at noon -> leave it off
    [TestCase(false, false, true, HoursStep.None)]     // started by hand in the evening -> leave it on
    [TestCase(false, true, true, HoursStep.None)]      // already on at 07:00
    public void Acts_only_when_the_hours_change(bool wasOpen, bool isOpen, bool running, HoursStep expected) =>
        HoursPolicy.Decide(wasOpen, isOpen, running).Should().Be(expected);

    [TestCase(false, true, HoursStep.Stop)]            // Kontrol restarted at night, app runs -> close it
    [TestCase(true, false, HoursStep.None)]            // Kontrol restarted at 10, app off -> never starts on its own
    [TestCase(true, true, HoursStep.None)]
    public void After_a_Kontrol_restart(bool isOpen, bool running, HoursStep expected) =>
        HoursPolicy.Decide(null, isOpen, running).Should().Be(expected);
}
