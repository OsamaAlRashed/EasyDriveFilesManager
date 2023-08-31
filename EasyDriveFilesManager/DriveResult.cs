using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasyDriveFilesManager
{
    public static class DriveResult
    {
        public static DriveResult<T> Success<T>(T result)
            => DriveResult<T>.Success(result);

        public static DriveResult<T> Failed<T>(string message)
            => DriveResult<T>.Failed(message);

        public static DriveResult<T> Failed<T>(Exception exception)
            => DriveResult<T>.Failed(exception);

        public static DriveResult<List<T>> Aggregate<T>(List<DriveResult<T>> results)
        {
            var finalResult = new DriveResult<List<T>>()
            {
                Result = new List<T>()
            };

            foreach (var result in results)
            {
                if (!result.IsSucceeded)
                    return Failed<List<T>>(result.Message);
                
                finalResult.Result.Add(result.Result);
            }

            return finalResult;
        }

        public static async Task<T> ResultAsync<T>(this Task<DriveResult<T>> result)
            => result is null ? default : (await result).Result;

    }

    public sealed class DriveResult<T> 
    {
        public T Result { get; set; }
        public Exception Exception { get; set; }
        public ResultType Type { get; set; }
        public string Message { get; set; }

        public bool IsSucceeded => Type == ResultType.Success;

        public static DriveResult<T> Success(T data)
            => new DriveResult<T>() { Result = data };

        public static DriveResult<T> Failed(string message)
           => new DriveResult<T>() { Message = message, Type = ResultType.Failed };

        public static DriveResult<T> Failed(Exception exception)
            => new DriveResult<T>() { Message = exception.Message, Exception = exception, Type = ResultType.Failed };

        public static implicit operator DriveResult<T>(T result)
            => Success(result);

    }

    public enum ResultType
    {
        Success,
        Failed
    }
}
