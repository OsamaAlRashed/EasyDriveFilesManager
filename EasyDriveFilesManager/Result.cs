using System;
using System.Collections.Generic;

namespace EasyDriveFilesManager
{
    public static class Result
    {
        public static Result<T> Success<T>(T result)
            => Result<T>.Success(result);

        public static Result<T> Failed<T>(string message)
            => Result<T>.Failed(message);

        public static Result<T> Failed<T>(Exception exception)
            => Result<T>.Failed(exception);

        public static Result<List<T>> Aggregate<T>(List<Result<T>> results)
        {
            var finalResult = new Result<List<T>>()
            {
                Data = new List<T>()
            };
            foreach (var result in results)
            {
                if (result.IsSucceded)
                    finalResult.Data.Add(result.Data);
                else
                    return Failed<List<T>>(result.Message);
            }

            return finalResult;
        }

    }

    public sealed class Result<T> 
    {
        public T Data { get; set; }
        public Exception Exception { get; set; }
        public ResultType Type { get; set; }
        public string Message { get; set; }

        public bool IsSucceded => Type == ResultType.Success;

        public static Result<T> Success(T data)
            => new Result<T>() { Data = data };

        public static Result<T> Failed(string message)
           => new Result<T>() { Message = message, Type = ResultType.Failed };

        public static Result<T> Failed(Exception exception)
            => new Result<T>() { Message = exception.Message, Exception = exception, Type = ResultType.Failed };

        public static implicit operator Result<T>(T result)
            => Success(result);

    }

    public enum ResultType
    {
        Success,
        Failed
    }
}
