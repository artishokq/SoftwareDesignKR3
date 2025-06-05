using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.APIGatewayTests;

/// <summary>
/// Fake HttpMessageHandler, который возвращает заранее заданный HttpResponseMessage или бросает исключение при SendAsync
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage? _response;
    private readonly Exception? _exception;

    /// <summary>
    /// Если response != null, SendAsync всегда вернёт его
    /// Если exception != null, SendAsync выбросит это исключение
    /// Если оба null, вернёт 200 OK без содержимого
    /// </summary>
    public FakeHttpMessageHandler(HttpResponseMessage? response = null, Exception? exception = null)
    {
        _response = response;
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_exception is not null)
        {
            throw _exception;
        }

        if (_response is not null)
        {
            return Task.FromResult(_response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}