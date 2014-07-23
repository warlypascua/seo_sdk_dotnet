﻿/*
 * ===========================================================================
 * Copyright 2014 Bazaarvoice, Inc.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * ===========================================================================
 * 
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Threading;
using BVSeoSdkDotNet.Model;
using BVSeoSdkDotNet.Config;
using BVSeoSdkDotNet.Util;
using BVSeoSdkDotNet.Url;
using BVSeoSdkDotNet.BVException;

namespace BVSeoSdkDotNet.Content
{
    /// <summary>
    /// Implementation class for BVUIContentService
    /// 
    /// @author Mohan Krupanandan
    /// </summary>
    public class BVUIContentServiceProvider : BVUIContentService
    {
        private BVConfiguration _bvConfiguration;
        private BVParameters _bvParameters;
        private StringBuilder _message;
        private StringBuilder _uiContent;
        private BVSeoSdkUrl _bvSeoSdkUrl;
        private Boolean sdkEnabled;

        /// <summary>
        /// Default Constructor to set the BVConfiguration values
        /// </summary>
        /// <param name="bvConfiguration"></param>
        public BVUIContentServiceProvider(BVConfiguration bvConfiguration)
        {
            _bvConfiguration = bvConfiguration;

            _message = new StringBuilder();
            _uiContent = new StringBuilder();
        }

        private void getBvContent(StringBuilder sb, Uri seoContentUrl, String baseUri)
        {
            if (isContentFromFile())
            {
                sb.Append(loadContentFromFile(seoContentUrl));
            }
            else
            {
                sb.Append(loadContentFromHttp(seoContentUrl));
            }

            BVUtilty.replaceString(sb, BVConstant.INCLUDE_PAGE_URI, baseUri + (baseUri.Contains("?") ? "&" : "?"));
        }

        private Boolean isContentFromFile()
        {
            Boolean loadFromFile = Boolean.Parse(_bvConfiguration.getProperty(BVClientConfig.LOAD_SEO_FILES_LOCALLY));
            return loadFromFile;
        }

        private String loadContentFromHttp(Uri path) 
        {
            int connectionTimeout = int.Parse(_bvConfiguration.getProperty(BVClientConfig.CONNECT_TIMEOUT));
            int socketTimeout = int.Parse(_bvConfiguration.getProperty(BVClientConfig.SOCKET_TIMEOUT));
            int proxyPort = int.Parse(_bvConfiguration.getProperty(BVClientConfig.PROXY_PORT));
            String proxyHost = _bvConfiguration.getProperty(BVClientConfig.PROXY_HOST);
            String content = null;
            Encoding encoding = Encoding.UTF8;

            try
            {
                String charsetConfig = _bvConfiguration.getProperty(BVClientConfig.CHARSET);
                encoding = String.IsNullOrEmpty(charsetConfig) ? Encoding.UTF8 : Encoding.GetEncoding(charsetConfig);
            }
            catch(Exception e)
            {
                encoding = Encoding.UTF8;
            }

            try
            {
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(path);
                httpRequest.Timeout = connectionTimeout;
                httpRequest.ReadWriteTimeout = socketTimeout;

                if (!String.IsNullOrEmpty(proxyHost) && !proxyHost.Equals("none", StringComparison.InvariantCultureIgnoreCase))
                {
                    WebProxy proxy = new WebProxy(proxyHost, proxyPort);
                    httpRequest.Proxy = proxy;
                }

                HttpWebResponse webResponse = (HttpWebResponse)httpRequest.GetResponse();
                Stream responseStream = webResponse.GetResponseStream();

                using (StreamReader reader = new StreamReader(responseStream,encoding))
                {
                    content = reader.ReadToEnd();
                }

                responseStream.Close();
                webResponse.Close();

            }
            catch (ProtocolViolationException e)
            {
                throw new BVSdkException("ERR0012");
            }
            catch (IOException e)
            {
                throw new BVSdkException("ERR0019");
            }
            catch (WebException e)
            {
                throw new BVSdkException("ERR0012");
            }
            catch (Exception e)
            {
                throw new BVSdkException(e.Message);
            }

            bool isValidContent = BVUtilty.validateBVContent(content);
            if (!isValidContent)
            {
                throw new BVSdkException("ERR0025");
            }


            return content;
        }

        private String loadContentFromFile(Uri path)
        {
            String content = null;
            Encoding encoding = Encoding.UTF8;

            try
            {
                String charsetConfig = _bvConfiguration.getProperty(BVClientConfig.CHARSET);
                encoding = String.IsNullOrEmpty(charsetConfig) ? Encoding.UTF8 : Encoding.GetEncoding(charsetConfig);
            }
            catch (Exception e)
            {
                encoding = Encoding.UTF8;
            }

            try
            {
                if (File.Exists(path.AbsolutePath))
                {
                    content = File.ReadAllText(path.AbsolutePath, encoding);
                }
                else
                {
                    throw new BVSdkException("ERR0012");
                }
            }
            catch (IOException e)
            {
                throw new BVSdkException("ERR0012");
            }

            return content;
        }

        private void includeIntegrationCode() 
        {
            String includeScriptStr = _bvConfiguration.getProperty(BVClientConfig.INCLUDE_DISPLAY_INTEGRATION_CODE);
            Boolean includeIntegrationScript = Boolean.Parse(includeScriptStr);

            if (!includeIntegrationScript) 
            {
                return;
            }

            Object[] parameters = {_bvParameters.SubjectType.uriValue(), _bvParameters.SubjectId};
            String integrationScriptValue = _bvConfiguration.getProperty(_bvParameters.ContentType.getIntegrationScriptProperty());
            String integrationScript = String.Format(integrationScriptValue, parameters);

            _uiContent.Append(integrationScript);
        }

        /// <summary>
        /// Gets a boolean value whether to Show UserAgent SEO Content
        /// </summary>
        /// <returns>A boolean value</returns>
        public Boolean showUserAgentSEOContent()
        {
            if (_bvParameters == null || String.IsNullOrEmpty(_bvParameters.UserAgent))
            {
                return false;
            }

            String crawlerAgentPattern = _bvConfiguration.getProperty(BVClientConfig.CRAWLER_AGENT_PATTERN);
            if (!String.IsNullOrEmpty(crawlerAgentPattern))
            {
                crawlerAgentPattern = ".*(" + crawlerAgentPattern + ").*";
            }
            Regex pattern = new Regex(crawlerAgentPattern, RegexOptions.IgnoreCase);
            
            return (pattern.IsMatch(_bvParameters.UserAgent) || _bvParameters.UserAgent.ToLower().Contains("google"));
        }

        /// <summary>
        /// Sets the BV Parameters for retreiving the SEO content
        /// </summary>
        /// <param name="bvParameters">Parameters that decide the SEO Content</param>
        public void setBVParameters(BVParameters bvParameters)
        {
            _bvParameters = bvParameters;
        }

        /// <summary>
        /// Set the URL for the SEO SDK.
        /// </summary>
        /// <param name="bvSeoSdkUrl"></param>
        public void setBVSeoSdkUrl(BVSeoSdkUrl bvSeoSdkUrl)
        {
            _bvSeoSdkUrl = bvSeoSdkUrl;
        }

        /// <summary>
        /// Implementation to check if sdk is enabled/disabled.
        /// The settings are based on the configurations from BVConfiguration and BVParameters.
        /// </summary>
        /// <returns>A Boolean value, true if sdk is enabled and false if it is not enabled</returns>
        public Boolean isSdkEnabled()
        {
            sdkEnabled = Boolean.Parse(_bvConfiguration.getProperty(BVClientConfig.SEO_SDK_ENABLED));
            sdkEnabled = sdkEnabled || _bvSeoSdkUrl.queryString().Contains(BVConstant.BVREVEAL);
            return sdkEnabled;
        }

        private void call() 
        {
            String displayJSOnly = null;
            Uri seoContentUrl = null;
            try 
            {
                //includes integration script if one is enabled.
                includeIntegrationCode();

                Boolean isBotDetection = Boolean.Parse(_bvConfiguration.getProperty(BVClientConfig.BOT_DETECTION));

                /*
                 * Hit only when botDetection is disabled or if the queryString is appended with bvreveal or if it matches any 
                 * crawler pattern that is configured at the client configuration. 
                 */
                if (!isBotDetection || _bvSeoSdkUrl.queryString().Contains(BVConstant.BVREVEAL) || showUserAgentSEOContent()) 
                {
                    seoContentUrl = _bvSeoSdkUrl.seoContentUri();
                    String correctedBaseUri = _bvSeoSdkUrl.correctedBaseUri();
                    getBvContent(_uiContent, seoContentUrl, correctedBaseUri);
                } 
                else 
                {
                    displayJSOnly = BVConstant.JS_DISPLAY_MSG;
                }
            } 
            catch (BVSdkException e) 
            {
                _message.Append(e.getMessage());
            }

            if (displayJSOnly != null) 
            {
                _message.Append(displayJSOnly);
            }
            
            //return _uiContent;
        }

        private bool RunWithTimeout(ThreadStart threadStart, TimeSpan timeout)
        {
            Thread workerThread = new Thread(threadStart);

            workerThread.Start();

            bool finished = workerThread.Join(timeout);
            if (!finished)
                workerThread.Abort();

            return finished;
        }

        /// <summary>
        /// Executes the server side call or the file level call within a specified execution timeout.
        /// when reload is set true then it gives from the cache that was already executed in the previous call.
        /// </summary>
        /// <param name="reload">A Boolean value to determine whether to reload from cache</param>
        /// <returns>A StringBuilder object representing the Content</returns>
        public StringBuilder executeCall(Boolean reload) 
        {
            if (reload) 
            {
                return new StringBuilder(_uiContent.ToString());
            }

            long executionTimeout = long.Parse(_bvConfiguration.getProperty(BVClientConfig.EXECUTION_TIMEOUT));

            try
            {
                Boolean fCallFinished;
                fCallFinished = RunWithTimeout(call, TimeSpan.FromMilliseconds(executionTimeout));
                if (!fCallFinished) _message.Append(String.Format(BVMessageUtil.getMessage("ERR0018"), new Object[] { executionTimeout }));
            }
            catch (ThreadInterruptedException e)
            {
                _message.Append(e.Message);
            }
            catch (ExecutionEngineException e)
            {
                _message.Append(e.Message);
            }
            catch (TimeoutException e)
            {
                _message.Append(String.Format(BVMessageUtil.getMessage("ERR0018"), new Object[] { executionTimeout }));
            }
            catch (Exception e)
            {
                throw new BVSdkException(e.Message);
            }

            return new StringBuilder(_uiContent.ToString());
        }

        /// <summary>
        /// Gets the messages if there are any after executeCall is invoked or if it is still in the cache.
        /// </summary>
        /// <returns>A StringBuilder object containing the messages if any</returns>
        public StringBuilder getMessage()
        {
            return _message;
        }
    }
}
