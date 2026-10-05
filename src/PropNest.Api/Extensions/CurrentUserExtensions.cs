using PropNest.Application.Abstractions;

namespace PropNest.Api.Extensions
{
    public static class CurrentUserExtensions 
    {
        public static void EnsureOwner(this ICurrentUser currentUser, long ownerUserId)
        {
            if (!currentUser.UserId.HasValue || currentUser.UserId.Value != ownerUserId)
            {
                throw new UnauthorizedAccessException("The current user is not the owner of the resource.");
            }
        }

        public static void EnsureCanViewHistory(this ICurrentUser currentUser, long ownerUserId)
        {
            var isOwner = currentUser.UserId.HasValue && currentUser.UserId.Value == ownerUserId;
            var isStaff = currentUser.IsInRole("Moderator") || currentUser.IsInRole("Admin");
            if (!isOwner && !isStaff)
            {
                throw new UnauthorizedAccessException("Only the owner, moderator, or admin can view this listing's history.");
            }
        }
    }
}
