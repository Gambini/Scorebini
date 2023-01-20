using System.Net;
using System.Runtime.CompilerServices;

namespace ScorebiniTwitchApi
{

    internal static class ApiResult
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ApiResult<T> New<T>(T Obj, int Code, string? Message) =>
            new ApiResult<T>(Obj, Code, Message);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ApiResult<T> New<T>(T Obj, HttpStatusCode Code, string? Message) =>
            new ApiResult<T>(Obj, Code, Message);
    }

    internal record struct ApiResult<T>(T Obj, int Code, string? Message)
    {
        public ApiResult(T obj, HttpStatusCode code, string? message)
            : this(obj, (int)code, message) { }
    }
}
