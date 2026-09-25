using FluentAssertions;
using ITMartinUpload.Server.Services;

namespace ITMartinUpload.Tests;

public class UploadOptionsTests
{
    private static UploadOptions Options() => new()
    {
        Token = "faelles",
        Customers =
        [
            new UploadCustomer { Slug = "annette-kent", Name = "Annette og Kent", Token = "kolibri-2026" },
            new UploadCustomer { Slug = "bogshoppen", Name = "Bogshoppen" },
            new UploadCustomer { Slug = "messe1", Name = "Messe1", Public = true },
        ],
    };

    [Test]
    public void A_public_event_opens_without_a_code()
    {
        Options().IsAuthorized("messe1", null).Should().BeTrue();
        Options().IsAuthorized("messe1", "anything").Should().BeTrue();
    }

    [Test]
    public void A_public_event_does_not_open_the_other_customers() =>
        Options().IsAuthorized("annette-kent", null).Should().BeFalse();

    [Test]
    public void Find_is_case_insensitive() =>
        Options().Find("Annette-Kent")!.Name.Should().Be("Annette og Kent");

    [Test]
    public void Find_returns_null_for_an_unknown_slug() =>
        Options().Find("ukendt").Should().BeNull();

    [Test]
    public void A_customer_token_wins_over_the_shared_one() =>
        Options().ExpectedToken("annette-kent").Should().Be("kolibri-2026");

    [Test]
    public void A_customer_without_its_own_token_uses_the_shared_one() =>
        Options().ExpectedToken("bogshoppen").Should().Be("faelles");

    [Test]
    public void One_customers_link_does_not_open_another_customers_folder() =>
        Options().IsAuthorized("bogshoppen", "kolibri-2026").Should().BeFalse();

    [Test]
    public void The_right_link_is_authorized() =>
        Options().IsAuthorized("annette-kent", "kolibri-2026").Should().BeTrue();

    [Test]
    public void Without_any_token_configured_everything_is_open() =>
        new UploadOptions().IsAuthorized("annette-kent", null).Should().BeTrue();

    [Test]
    public void Greeting_uses_the_customer_name() =>
        Options().Find("annette-kent")!.GreetingOrDefault.Should().Be("Hej Annette og Kent!");

    [Test]
    public void Greeting_can_be_overridden_per_customer() =>
        new UploadCustomer { Name = "Annette og Kent", Greeting = "Godmorgen, I to!" }
            .GreetingOrDefault.Should().Be("Godmorgen, I to!");

    [Test]
    public void Greeting_falls_back_when_we_know_nothing_about_the_customer() =>
        new UploadCustomer().GreetingOrDefault.Should().Be("Velkommen!");
}
