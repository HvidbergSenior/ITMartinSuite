namespace ITMartinFamily.Application.Interfaces;

public interface IPushNotificationService
{
    string GetPublicKey();
    Task SendToFamilyAsync(Guid familyId, string excludeMember, string title, string body);
    Task SendToMemberAsync(Guid familyId, string memberName, string title, string body);
}
