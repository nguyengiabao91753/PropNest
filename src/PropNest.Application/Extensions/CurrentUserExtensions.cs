using PropNest.Application.Abstractions;

namespace PropNest.Application.Extensions
{
    public static class CurrentUserExtensions
    {
        private static bool IsOwner(this ICurrentUser currentUser, long ownerUserId)
        {
            return currentUser.UserId.HasValue && currentUser.UserId.Value == ownerUserId;
        }

        public static void EnsureOwner(this ICurrentUser currentUser, long ownerUserId)
        {
            if (!IsOwner(currentUser, ownerUserId))
            {
                throw new UnauthorizedAccessException("The current user is not the owner of the resource.");
            }
        }
    }
}
