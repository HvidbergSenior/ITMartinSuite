using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class PickRotationTests
{
    private static Dictionary<int, RotationFaces> R(float s0, float s90, float s180, float s270) => new()
    {
        [0] = new(s0 > 0 ? 1 : 0, s0), [90] = new(s90 > 0 ? 1 : 0, s90),
        [180] = new(s180 > 0 ? 1 : 0, s180), [270] = new(s270 > 0 ? 1 : 0, s270),
    };

    [Test] public void Upright_photo_is_decided_as_zero() => LibraryPolishService.PickRotation(R(0.95f, 0, 0, 0)).Should().Be(0);
    [Test] public void Sideways_photo_decided_by_the_clearly_best_rotation() => LibraryPolishService.PickRotation(R(0.40f, 0.92f, 0.35f, 0.45f)).Should().Be(90);
    [Test] public void Weak_faces_at_every_rotation_stay_undecided() => LibraryPolishService.PickRotation(R(0.5f, 0.55f, 0.5f, 0.52f)).Should().BeNull();
    [Test] public void Two_rotations_close_together_stay_undecided() => LibraryPolishService.PickRotation(R(0.9f, 0.85f, 0.2f, 0.1f)).Should().BeNull();
    [Test] public void No_faces_is_undecided() => LibraryPolishService.PickRotation(R(0, 0, 0, 0)).Should().BeNull();
}

[TestFixture]
public class PickRotationFromRollsTests
{
    private static FaceRoll F(float score, float roll, int size = 300) => new(score, roll, size);

    [Test] public void Upright_face_means_no_rotation() => LibraryPolishService.PickRotationFromRolls([F(0.98f, 9f)]).Should().Be(0);
    [Test] public void Face_rolled_plus_86_needs_90_clockwise() => LibraryPolishService.PickRotationFromRolls([F(0.98f, 85.7f)]).Should().Be(90);
    [Test] public void Face_rolled_minus_78_needs_270_clockwise() => LibraryPolishService.PickRotationFromRolls([F(0.98f, -77.9f)]).Should().Be(270);
    [Test] public void Upside_down_face_needs_180() => LibraryPolishService.PickRotationFromRolls([F(0.98f, 175f)]).Should().Be(180);
    [Test] public void Tilted_head_is_not_evidence() => LibraryPolishService.PickRotationFromRolls([F(0.98f, 45f)]).Should().BeNull();
    [Test] public void Low_confidence_faces_do_not_vote() => LibraryPolishService.PickRotationFromRolls([F(0.5f, 88f)]).Should().BeNull();
    [Test] public void Disagreeing_big_faces_leave_it_undecided() => LibraryPolishService.PickRotationFromRolls([F(0.98f, 2f), F(0.98f, 88f)]).Should().BeNull();
    [Test] public void A_small_bystander_cannot_outvote_the_subject() => LibraryPolishService.PickRotationFromRolls([F(0.98f, 88f, 400), F(0.98f, 3f, 60)]).Should().Be(90);
}
