﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thinktecture.IdentityModel.Constants;
using Thinktecture.IdentityServer.Models;
using Thinktecture.IdentityServer.Protocols.OAuth2;
using Thinktecture.IdentityServer.Repositories;


namespace Thinktecture.IdentityServer.Protocols.OpenIdConnect
{
    public class AuthorizeRequestValidator
    {
        [Import]
        public IClientsRepository Clients { get; set; }

        public AuthorizeRequestValidator(IClientsRepository clients)
        {
            Clients = clients;
        }

        public ValidatedRequest Validate(AuthorizeRequest request)
        {
            var validatedRequest = new ValidatedRequest();

            // validate request model binding
            if (request == null)
            {
                throw new AuthorizeRequestResourceOwnerException("Invalid request parameters.");
            }

            // make sure redirect uri is present
            if (string.IsNullOrWhiteSpace(request.redirect_uri))
            {
                throw new AuthorizeRequestResourceOwnerException("Missing redirect URI");
            }

            // validate client
            if (string.IsNullOrWhiteSpace(request.client_id))
            {
                throw new AuthorizeRequestResourceOwnerException("Missing client identifier");
            }

            Client client;
            if (!Clients.TryGetClient(request.client_id, out client))
            {
                throw new AuthorizeRequestResourceOwnerException("Invalid client: " + request.client_id);
            }

            validatedRequest.Client = client;
            Tracing.InformationFormat("Client: {0} ({1})",
                validatedRequest.Client.Name,
                validatedRequest.Client.ClientId);

            // make sure redirect_uri is a valid uri, and in case of http is over ssl
            Uri redirectUri;
            if (Uri.TryCreate(request.redirect_uri, UriKind.Absolute, out redirectUri))
            {
                if (redirectUri.Scheme == Uri.UriSchemeHttp)
                {
                    throw new AuthorizeRequestClientException(
                        "Redirect URI not over SSL : " + request.redirect_uri,
                        new Uri(request.redirect_uri),
                        OAuth2Constants.Errors.InvalidRequest,
                        string.Empty,
                        validatedRequest.State);
                }

                // make sure redirect uri is registered with client
                if (!request.redirect_uri.Equals(validatedRequest.Client.RedirectUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase))
                {
                    throw new AuthorizeRequestResourceOwnerException("Invalid redirect URI: " + request.redirect_uri);
                }

                validatedRequest.RedirectUri = request.redirect_uri;
                Tracing.InformationFormat("Redirect URI: {0}",
                    validatedRequest.RedirectUri);                    
            }
            else
            {
                throw new AuthorizeRequestResourceOwnerException("Invalid redirect URI: " + request.redirect_uri);
            }

            // check state
            if (!string.IsNullOrWhiteSpace(request.state))
            {
                validatedRequest.State = request.state;
                Tracing.Information("State: " + validatedRequest.State);
            }
            else
            {
                Tracing.Information("No state supplied.");
            }

            // validate response type
            if (String.IsNullOrWhiteSpace(request.response_type))
            {
                throw new AuthorizeRequestClientException(
                    "response_type is null or empty",
                    new Uri(validatedRequest.RedirectUri),
                    OAuth2Constants.Errors.InvalidRequest,
                    string.Empty,
                    validatedRequest.State);
            }

            // check response type (only code and token are supported)
            if (!request.response_type.Equals(OAuth2Constants.ResponseTypes.Token, StringComparison.Ordinal) &&
                !request.response_type.Equals(OAuth2Constants.ResponseTypes.Code, StringComparison.Ordinal))
            {
                throw new AuthorizeRequestClientException(
                    "response_type is not token or code: " + request.response_type,
                    new Uri(validatedRequest.RedirectUri),
                    OAuth2Constants.Errors.UnsupportedResponseType,
                    string.Empty,
                    validatedRequest.State);
            }

            validatedRequest.ResponseType = request.response_type;
            Tracing.Information("Response type: " + validatedRequest.ResponseType);

            // validate scopes
            if (!request.scope.StartsWith("openid"))
            {
                throw new AuthorizeRequestClientException(
                    "Invalid scope: " + request.scope,
                    new Uri(validatedRequest.RedirectUri),
                    OAuth2Constants.Errors.InvalidScope,
                    validatedRequest.ResponseType,
                    validatedRequest.State);
            }

            var scopes = request.scope.Split(' ');
            if (scopes.Length == 1)
            {
                throw new AuthorizeRequestClientException(
                    "Invalid scopes: " + request.scope,
                    new Uri(validatedRequest.RedirectUri),
                    OAuth2Constants.Errors.InvalidScope,
                    validatedRequest.ResponseType,
                    validatedRequest.State);
            }

            validatedRequest.Scopes = request.scope; 


            if (request.response_type == OAuth2Constants.ResponseTypes.Code)
            {
                ValidateCodeResponseType(validatedRequest, request);
            }
            else if (request.response_type == OAuth2Constants.ResponseTypes.Token)
            {
                ValidateTokenResponseType(validatedRequest, request);
            }
            else
            {
                throw new AuthorizeRequestClientException(
                    "Invalid response_type: " + request.response_type,
                    new Uri(validatedRequest.RedirectUri),
                    OAuth2Constants.Errors.UnsupportedResponseType,
                    request.response_type,
                    validatedRequest.State);
            }

            return validatedRequest;
        }

        private void ValidateTokenResponseType(ValidatedRequest validatedRequest, AuthorizeRequest request)
        {
            throw new NotImplementedException();
        }

        private void ValidateCodeResponseType(ValidatedRequest validatedRequest, AuthorizeRequest request)
        {
            if (!validatedRequest.Client.AllowCodeFlow)
            {
                throw new AuthorizeRequestClientException(
                   "response_type is not allowed: " + request.response_type,
                   new Uri(validatedRequest.RedirectUri),
                   OAuth2Constants.Errors.UnsupportedResponseType,
                   request.response_type,
                   validatedRequest.State);
            }
        }
    }
}
