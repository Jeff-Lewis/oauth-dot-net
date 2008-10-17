﻿// Copyright (c) 2008 Madgex
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// OAuth.net uses the Common Service Locator interface, released under the MS-PL
// license. See "CommonServiceLocator License.txt" in the Licenses folder.
// 
// The examples and test cases use the Windsor Container from the Castle Project
// and Common Service Locator Windsor adaptor, released under the Apache License,
// Version 2.0. See "Castle Project License.txt" in the Licenses folder.
// 
// XRDS-Simple.net uses the HTMLAgility Pack. See "HTML Agility Pack License.txt"
// in the Licenses folder.
//
// Authors: Bruce Boughton, Chris Adams
// Website: http://lab.madgex.com/oauth-net/
// Email:   oauth-dot-net@madgex.com

using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using OAuth.Net.Common;

namespace OAuth.Net.Consumer
{
    /// <summary>
    /// Makes a request to an OAuth protected resource.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To create an <see cref="OAuthRequest"/>, use the <see cref="Create"/> 
    /// methods. You should pass a configured <see cref="OAuthService"/> which
    /// encapsulated the protocol configuration details.
    /// </para>
    /// 
    /// <para>
    /// When constructing an <see cref="OAuthRequest"/>, you should pass the 
    /// request and/or access tokens if you have already performed user 
    /// authorization.
    /// </para>
    /// 
    /// <para>
    /// If a valid access token is supplied, this token will be used to request
    /// the protected resource when <see cref="GetResource"/> is called and the
    /// returned <see cref="OAuthResponse"/> should contain the resource 
    /// representation as an <see cref="OAuthResource"/>. If not, 
    /// <see cref="OAuthResponse.HasProtectedResource"/> will be <c>false</c>.
    /// </para>
    /// 
    /// <para>
    /// If no access token is supplied, but an authorized request token is 
    /// passed, calling <see cref="GetResource"/> will first request an access
    /// token. If successful, the protected resource request will then proceed.
    /// The returned <see cref="OAuthResponse"/> will contain the access token
    /// obtained in its <see cref="OAuthResponse.Token"/> property and the
    /// resource representation as a <see cref="OAuthResource"/>. 
    /// <see cref="OAuthResponse.HasProtectedResource"/> will be <c>true</c>.
    /// </para>
    /// 
    /// <para>
    /// If no tokens are supplied at all, or if the request token supplied is
    /// not authenticated (and no access token is supplied), then the user will
    /// be directed to perform authorization of the request. The 
    /// <see cref="OAuthResponse.HasProtectedResource"/> property will be 
    /// <c>false</c>. If a request token has been obtained, it will be stored 
    /// in the <see cref="OAuthResponse.Token"/> property. You should direct the
    /// user to the authorization URL as generated by 
    /// <see cref="OAuthService.BuildAuthorizationUrl"/> (passing in the request 
    /// token and optionally a callback URL and/or additional parameters). Once
    /// the user has completed the authorization step, you should re-submit the
    /// <see cref="OAuthRequest"/> with the now authenticated response token
    /// and proceed as above.
    /// </para>
    /// 
    /// <para>
    /// If the service provider supports the Problem Reporting extension and
    /// an error occurs, a report will be thrown as a 
    /// <see cref="OAuthRequestException"/>. You should use this to inform your
    /// response to this error.
    /// </para>
    /// 
    /// <example>
    /// The following example is taken from the article, 
    /// <see href="http://lab.madgex.com/oauth-net/gettingatarted01.aspx/">
    /// Getting Started: Building a Fire Eagle consumer with OAuth.net</see>.
    /// 
    /// <code>
    /// // Find the user's location
    /// var request = OAuthRequest.Create(
    ///     new Uri("https://fireeagle.yahooapis.com/api/0.1/user"),
    ///     FireEagle.GetService,
    ///     context.Session["request_token"] as IToken,
    ///     context.Session["access_token"] as IToken);
    /// OAuthResponse response = request.GetResource();
    /// 
    /// if (response.HasProtectedResource)
    /// {
    ///     // Store the access token
    ///     context.Session["access_token"] = response.Token;
    ///  
    ///  
    ///     // Load the response XML
    ///     XmlDocument responseXml = new XmlDocument();
    ///     responseXml.Load(response.ProtectedResource.GetResponseStream());
    ///  
    ///  
    ///     // Check the response status
    ///     if (responseXml.SelectSingleNode("rsp/@stat").Value == "ok")
    ///  
    ///         return Location.Parse(responseXml.SelectSingleNode(
    ///             "rsp/user/location-hierarchy/location[@best-guess='true']"));
    ///     else
    ///         return null;
    /// }
    /// else
    /// {
    ///     // Authorization is required
    ///     context.Session["request_token"] = response.Token;
    ///     throw new AuthorizationRequiredException()
    ///     {
    ///         AuthorizationUri = FireEagle.GetService.BuildAuthorizationUrl(
    ///             response.Token, 
    ///             callback)
    ///     };
    /// }
    /// </code>
    /// </example>
    /// 
    /// </remarks>
    public class OAuthRequest
    {
        /// <summary>
        /// This event is fired before the request to get a request token is created. You may
        /// modify the request Uri and HTTP method, as well as add additional request 
        /// parameters to be sent with the request in the query string or post body.
        /// </summary>
        public event EventHandler<PreRequestEventArgs> OnBeforeGetRequestToken;

        /// <summary>
        /// This event is fired after the request token has been received. If an 
        /// exception occurs receiving the response, this event will not fire.
        /// </summary>
        public event EventHandler<RequestTokenReceivedEventArgs> OnReceiveRequestToken;

        /// <summary>
        /// This event is fired before the request to get an access token is created. You may
        /// modify the request Uri and HTTP method.
        /// </summary>
        public event EventHandler<PreAccessTokenRequestEventArgs> OnBeforeGetAccessToken;

        /// <summary>
        /// This event is fired after the access token has been received. If an 
        /// exception occurs receiving the response, this event will not fire.
        /// </summary>
        public event EventHandler<AccessTokenReceivedEventArgs> OnReceiveAccessToken;

        /// <summary>
        /// This event is fired before the request to get the protected resource is created.
        /// You may modify the request Uri and HTTP method, as well as add additional request 
        /// parameters to be sent with the request in the query string or post body.
        /// </summary>
        public event EventHandler<PreProtectedResourceRequestEventArgs> OnBeforeGetProtectedResource;

        /// <summary>
        /// The protected resource Uri
        /// </summary>
        public Uri ResourceUri
        {
            get;
            private set;
        }

        /// <summary>
        /// The OAuth service
        /// </summary>
        public OAuthService Service
        {
            get;
            private set;
        }

        /// <summary>
        /// The request token
        /// </summary>
        public IToken RequestToken
        {
            get;
            private set;
        }

        /// <summary>
        /// The access token
        /// </summary>
        public IToken AccessToken
        {
            get;
            private set;
        }

        /// <summary>
        /// This delegate, if not <c>null</c>, is called when the request token
        /// requires authorization.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It is not required to use this mechanism. Instead, you can inspect
        /// <see cref="OAuthResponse.HasProtectedResource"/> and direct the
        /// user to authorize if <c>false</c>.
        /// </para>
        /// 
        /// <para>
        /// If you supply a handler, it should direct the user to authorize in
        /// an application-specific manner. If this can be done without 
        /// aborting the current thread, 
        /// <see cref="AuthorizationEventArgs.ContinueOnReturn"/> should be set
        /// to <c>true</c> to indicate that the workflow can continue without
        /// re-submitting the <see cref="OAuthRequest"/>.
        /// </para>
        /// 
        /// <para>
        /// In either mechanism, the authorization URI is built using
        /// <see cref="OAuthService.BuildAuthorizationUrl"/>. If a callback URI
        /// is supplied, the service provider will redirect the user to it once
        /// the authorization process is complete. If additional parameters are
        /// supplied, these will be included in the authorization URI 
        /// generated.
        /// </para>
        /// </remarks>
        public EventHandler<AuthorizationEventArgs> AuthorizationHandler
        {
            get;
            set;
        }

        /// <summary>
        /// Creates a new OAuth protected request.
        /// </summary>
        /// <remarks>
        /// Since neither a request token nor an access token is supplied,
        /// the user will have to authorize this request.
        /// </remarks>
        /// <param name="resourceUri">Protected resource URI</param>
        /// <param name="settings">Service settings</param>
        /// <returns>An OAuth protected request for the protected resource</returns>
        public static OAuthRequest Create(Uri resourceUri, OAuthService settings)
        {
            return new OAuthRequest()
            {
                ResourceUri = resourceUri,
                Service = settings
            };
        }

        /// <summary>
        /// Creates a new OAuth protected request, initialised with a previously
        /// retrieved request token. This token may or may not have been authorized.
        /// </summary>
        /// <remarks>
        /// If the request token supplied has not been authorized, the user will
        /// have to be directed to authorize it before the request can proceed.
        /// </remarks>
        /// <param name="resourceUri">Protected resource URI</param>
        /// <param name="settings">Service settings</param>
        /// <param name="requestToken">Request token</param>
        /// <returns>An OAuth protected request for the protected resources,
        /// initialised with the request token</returns>
        public static OAuthRequest Create(Uri resourceUri, OAuthService settings, IToken requestToken)
        {
            return new OAuthRequest()
            {
                ResourceUri = resourceUri,
                Service = settings,
                RequestToken = requestToken
            };
        }

        /// <summary>
        /// Creates a new OAuth protected request, initialised with previously
        /// retrieved request and access tokens. 
        /// </summary>
        /// <remarks>
        /// If the access token is valid, the user should not have to intervene
        /// to authorize the request and the protected resource should be
        /// fetched immediately.
        /// </remarks>
        /// <param name="resourceUri">Protected resource URI</param>
        /// <param name="settings">Service settings</param>
        /// <param name="requestToken">Request token</param>
        /// <param name="accessToken">Access token</param>
        /// <returns>An OAuth protected request for the protected resource,
        /// initialised with the request token and access token</returns>
        public static OAuthRequest Create(Uri resourceUri, OAuthService settings, IToken requestToken, IToken accessToken)
        {
            return new OAuthRequest()
            {
                ResourceUri = resourceUri,
                Service = settings,
                RequestToken = requestToken,
                AccessToken = accessToken
            };
        }

        /// <exception cref="OAuth.Net.Common.OAuthRequestException">
        /// <list>
        /// <item>If the server responds with an OAuthRequestException</item>
        /// <item>If the server's responds unexpectedly</item>
        /// <item>If the requests to the server cannot be signed</item>
        /// </list>
        /// </exception>
        public OAuthResponse GetResource()
        {
            return this.GetResource(null);
        }

        /// <param name="parameters">Additional parameters to send with the protected resource request</param>
        /// <exception cref="OAuth.Net.Common.OAuthRequestException">
        /// <list>
        /// <item>If the server responds with an OAuthRequestException</item>
        /// <item>If the server's responds unexpectedly</item>
        /// <item>If the requests to the server cannot be signed</item>
        /// </list>
        /// </exception>
        public OAuthResponse GetResource(NameValueCollection parameters)
        {
            OAuthResponse response;

            HttpWebRequest request = this.PrepareProtectedResourceRequest(parameters);

            // A null value for the HttpWebRequest is returned when a ResponseToken is returned
            // and no one has returned in the AuthorizationHandler continue with getting an AccessToken
            // or an RequestToken exists but the AccessToken request was refused.
            if (request == null)
                response = new OAuthResponse(this.RequestToken);
            else
            {
                OAuthResource resource;
                OAuthParameters responseParameters;

                try
                {
                    resource = new OAuthResource((HttpWebResponse)request.GetResponse());

                    // Parse the parameters and re-throw any OAuthRequestException from the service provider
                    responseParameters = OAuthParameters.Parse(resource);
                    OAuthRequestException.TryRethrow(responseParameters);

                    // If nothing is thrown then we should have a valid resource.
                    response = new OAuthResponse(this.AccessToken ?? this.RequestToken, resource);
                }
                catch (WebException e)
                {
                    // Parse the parameters and re-throw any OAuthRequestException from the service provider
                    responseParameters = OAuthParameters.Parse(e.Response as HttpWebResponse);
                    OAuthRequestException.TryRethrow(responseParameters);

                    // If no OAuthRequestException, rethrow the WebException
                    #warning TODO: We have consumer the WebException's body so rethrowing it is pretty pointless; wrap the WebException in an OAuthProtocolException and store the body (create an OAuthResource before parsing parameters)
                    throw;
                }
            }

            return response;
        }

        /// <exception cref="OAuth.Net.Common.OAuthRequestException">
        /// <list>
        /// <item>If the server responds with an OAuthRequestException</item>
        /// <item>If the server's responds unexpectedly</item>
        /// <item>If the requests to the server cannot be signed</item>
        /// </list>
        /// </exception>
        protected virtual HttpWebRequest PrepareProtectedResourceRequest(NameValueCollection parameters)
        {
            if (this.AccessToken == null || this.RequestToken == null)
            {
                if (this.RequestToken == null)
                {
                    // Get a request token
                    this.DoGetRequestToken();
                }

                if (this.RequestToken == null)
                    throw new InvalidOperationException("Request token was not received.");

                if (this.RequestToken.Status != TokenStatus.Authorized)
                {
                    // Get the authorization handler to authorize the request token
                    // Halt processing if the authorization handler is out-of-band
                    if (!this.DoAuthorizeRequestToken())
                        return null;
                }

                if (this.RequestToken == null || this.RequestToken.Status != TokenStatus.Authorized)
                    throw new InvalidOperationException("Request token was not authorized.");

                // Get an access token
                this.DoGetAccessToken();
            }

            if (this.AccessToken == null)
                throw new InvalidOperationException("Access token was not received.");

            return this.DoPrepareProtectedResourceRequest(parameters);
        }

        protected virtual void DoGetRequestToken()
        {
            // Fire the OnBeforeGetRequestToken event
            PreRequestEventArgs args = new PreRequestEventArgs(
                this.Service.RequestTokenUrl, 
                this.Service.HttpMethod,
                new NameValueCollection());

            if (this.OnBeforeGetRequestToken != null)
                this.OnBeforeGetRequestToken(this, args);

            // Create and sign the request
            HttpWebRequest request = this.CreateAndSignRequest(
                args.RequestUri, 
                args.HttpMethod, 
                args.AdditionalParameters, 
                null);

            HttpWebResponse response = null;
            OAuthParameters responseParameters = null;

            // Get the service provider response
            try
            {
                response = (HttpWebResponse)request.GetResponse();

                // Parse the parameters and re-throw any OAuthRequestException from the service provider
                responseParameters = OAuthParameters.Parse(response);
                OAuthRequestException.TryRethrow(responseParameters);
            }
            catch (WebException e)
            {
                // Parse the parameters and re-throw any OAuthRequestException from the service provider
                responseParameters = OAuthParameters.Parse(e.Response as HttpWebResponse);
                OAuthRequestException.TryRethrow(responseParameters);

                // If no OAuthRequestException, rethrow the WebException
                throw;
            }
            
            // Store the request token
            this.RequestToken = new OAuthToken(
                TokenType.Request, 
                responseParameters.Token, 
                responseParameters.TokenSecret, 
                this.Service.Consumer);            

            // Fire the OnReceiveRequestToken event
            RequestTokenReceivedEventArgs responseArgs = new RequestTokenReceivedEventArgs(
                this.RequestToken, 
                responseParameters.AdditionalParameters);

            if (this.OnReceiveRequestToken != null)
                this.OnReceiveRequestToken(this, responseArgs);
        }

        /// <summary>
        /// Raises the AuthorizationEventArgs that allows a Consumer to determine 
        /// if the request should stop and return the RequestToken or 
        /// continue and request the access token.  This allow the a Consumer desktop 
        /// app to sleep the thread whilst the consumer goes elsewhere to perform
        /// the authorization.
        /// </summary>
        /// <returns></returns>
        protected virtual bool DoAuthorizeRequestToken()
        {
            if (this.RequestToken == null)
                throw new InvalidOperationException("Request token must be present");

            // There is no feedback from the SP as to whether the user granted/denied access so we assume they authorized the request token
            this.RequestToken.Status = TokenStatus.Authorized;
            
            // Invoke the authorization handler
            AuthorizationEventArgs authArgs = new AuthorizationEventArgs(this.RequestToken);

            if (this.AuthorizationHandler != null)                                            
                this.AuthorizationHandler(this, authArgs);                      

            return authArgs.ContinueOnReturn;
        }

        protected virtual void DoGetAccessToken()
        {
            // Fire the OnBeforeGetAccessToken event
            PreAccessTokenRequestEventArgs preArgs = new PreAccessTokenRequestEventArgs(
                this.Service.AccessTokenUrl,
                this.Service.HttpMethod, 
                this.RequestToken);

            if (this.OnBeforeGetAccessToken != null)
                this.OnBeforeGetAccessToken(this, preArgs);

            // Create and sign the request
            HttpWebRequest request = this.CreateAndSignRequest(
                preArgs.RequestUri, 
                preArgs.HttpMethod, 
                null, 
                this.RequestToken);

            HttpWebResponse response = null;
            OAuthParameters responseParameters = null;

            // Get the service provider response
            try
            {
                response = (HttpWebResponse)request.GetResponse();

                // Parse the parameters and re-throw any OAuthRequestException from the service provider
                responseParameters = OAuthParameters.Parse(response);
                OAuthRequestException.TryRethrow(responseParameters);
            }
            catch (WebException e)
            {
                // Parse the parameters and re-throw any OAuthRequestException from the service provider
                responseParameters = OAuthParameters.Parse(e.Response as HttpWebResponse);
                OAuthRequestException.TryRethrow(responseParameters);

                // If no OAuthRequestException, rethrow the WebException
                throw;
            }

            // Store the access token
            this.AccessToken = new OAuthToken(
                TokenType.Access, 
                responseParameters.Token,
                responseParameters.TokenSecret, 
                this.Service.Consumer);

            // Fire the OnReceiveAccessToken event
            AccessTokenReceivedEventArgs responseArgs = new AccessTokenReceivedEventArgs(
                this.RequestToken,
                this.AccessToken, 
                responseParameters.AdditionalParameters);

            if (this.OnReceiveAccessToken != null)
                this.OnReceiveAccessToken(this, responseArgs);
        }

        protected virtual HttpWebRequest DoPrepareProtectedResourceRequest(NameValueCollection parameters)
        {
            // Fire the OnBeforeGetProtectedResource event
            PreProtectedResourceRequestEventArgs preArgs = new PreProtectedResourceRequestEventArgs(
                this.ResourceUri, 
                this.Service.HttpMethod,
                parameters ?? new NameValueCollection(), 
                this.RequestToken, 
                this.AccessToken);

            if (this.OnBeforeGetProtectedResource != null)
                this.OnBeforeGetProtectedResource(this, preArgs);

            // Prepare request
            return this.CreateAndSignRequest(
                preArgs.RequestUri, 
                preArgs.HttpMethod, 
                preArgs.AdditionalParameters, 
                this.AccessToken);
        }

        protected virtual HttpWebRequest CreateAndSignRequest(Uri requestUri, string httpMethod, NameValueCollection additionalParameters, IToken token)
        {
            int timestamp = UnixTime.ToUnixTime(DateTime.Now);

            OAuthParameters authParameters = new OAuthParameters()
            {
                ConsumerKey = this.Service.Consumer.Key,
                Realm = this.Service.Realm,
                SignatureMethod = this.Service.SignatureMethod,
                Timestamp = timestamp.ToString(CultureInfo.InvariantCulture),
                Nonce = this.Service.ComponentLocator.GetInstance<INonceProvider>().GenerateNonce(timestamp),
                Version = this.Service.OAuthVersion
            };

            if (token != null)
                authParameters.Token = token.Token;

            if (additionalParameters != null && additionalParameters.Count > 0)
                authParameters.AdditionalParameters.Add(additionalParameters);

            // Normalize the request uri for signing
            if (!string.IsNullOrEmpty(requestUri.Query))
            {
                UriBuilder mutableRequestUri = new UriBuilder(requestUri);

                // TODO: Will the parameters necessarily be Rfc3698 encoded here? If not, then Rfc3968.SplitAndDecode will throw FormatException
                authParameters.AdditionalParameters.Add(Rfc3986.SplitAndDecode(mutableRequestUri.Query.Substring(1)));

                mutableRequestUri.Query = null;
                requestUri = mutableRequestUri.Uri;
            }

            // Check there is a signing provider for the signature method
            ISigningProvider signingProvider = this.Service.ComponentLocator.GetInstance<ISigningProvider>(Constants.SigningProviderIdPrefix + this.Service.SignatureMethod);

            if (signingProvider == null)
            {
                // There is no signing provider for this signature method
                OAuthRequestException.ThrowSignatureMethodRejected(null);
            }

            // Double check the signing provider declares that it can handle the signature method
            if (!signingProvider.SignatureMethod.Equals(this.Service.SignatureMethod))
                OAuthRequestException.ThrowSignatureMethodRejected(null);

            // Compute the signature
            authParameters.Signature = signingProvider.ComputeSignature(
                SignatureBase.Create(httpMethod, requestUri, authParameters),
                this.Service.Consumer.Secret,
                (token != null && token.Secret != null) ? token.Secret : null);

            // Create the request, attaching the OAuth parameters and additional parameters
            switch (httpMethod)
            {
                case "GET":
                    if (this.Service.UseAuthorizationHeader)
                    {
                        // Put the OAuth parameters in the header and the additional parameters in the query string
                        string authHeader = authParameters.ToHeaderFormat();
                        string query = Rfc3986.EncodeAndJoin(authParameters.AdditionalParameters);

                        if (!string.IsNullOrEmpty(query))
                        {
                            UriBuilder mutableRequestUri = new UriBuilder(requestUri);
                            if (string.IsNullOrEmpty(mutableRequestUri.Query))
                                mutableRequestUri.Query = query;
                            else
                                mutableRequestUri.Query = mutableRequestUri.Query.Substring(1) + "&" + query;

                            requestUri = mutableRequestUri.Uri;
                        }

                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
                        request.Method = httpMethod;
                        request.Headers.Add(HttpRequestHeader.Authorization, authHeader);
                        return request;
                    }
                    else 
                    {
                        string query = authParameters.ToQueryStringFormat();

                        UriBuilder mutableRequestUri = new UriBuilder(requestUri);
                        if (string.IsNullOrEmpty(mutableRequestUri.Query))
                            mutableRequestUri.Query = query;
                        else
                            mutableRequestUri.Query = mutableRequestUri.Query.Substring(1) + "&" + query;

                        requestUri = mutableRequestUri.Uri;

                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
                        request.Method = httpMethod;
                        return request;
                    }

                case "POST":
                    if (this.Service.UseAuthorizationHeader)
                    {
                        // Put the OAuth parameters in the header and the additional parameters in the post body
                        string authHeader = authParameters.ToHeaderFormat();
                        string body = Rfc3986.EncodeAndJoin(authParameters.AdditionalParameters);

                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
                        request.Method = httpMethod;

                        request.Headers.Add(HttpRequestHeader.Authorization, authHeader);

                        byte[] bodyBytes = Encoding.ASCII.GetBytes(body);
                        request.ContentType = Constants.HttpPostUrlEncodedContentType;
                        request.ContentLength = bodyBytes.Length;

                        Stream requestStream = request.GetRequestStream();
                        requestStream.Write(bodyBytes, 0, bodyBytes.Length);

                        return request;
                    }
                    else
                    {
                        string body = authParameters.ToNormalizedString(
                            Constants.RealmParameter,
                            Constants.TokenSecretParameter);

                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUri);
                        request.Method = httpMethod;

                        byte[] bodyBytes = Encoding.ASCII.GetBytes(body);
                        request.ContentType = Constants.HttpPostUrlEncodedContentType;
                        request.ContentLength = bodyBytes.Length;

                        Stream requestStream = request.GetRequestStream();
                        requestStream.Write(bodyBytes, 0, bodyBytes.Length);

                        return request;
                    }

                default:
                    throw new ArgumentException("httpMethod argument must be GET or POST", "httpMethod");
            }
        }
    }
}
