using System;

namespace HotChocolate.AspNetCore.Authorization
{
    /// <summary>
    /// Minimal local Authorize attribute stub to allow compilation. Replace with proper HotChocolate authorization package/config later.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter, Inherited = true, AllowMultiple = true)]
    public sealed class AuthorizeAttribute : Attribute
    {
        public AuthorizeAttribute()
        {
        }
    }
}
