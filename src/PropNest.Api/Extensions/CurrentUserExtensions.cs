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
    }
}
