using UnityEngine;

namespace AURAID.Voice
{
    /// <summary>
    /// Diagnoses HTTP 429 from OpenAI response body (rate limit vs quota).
    /// </summary>
    public static class OpenAi429Helper
    {
        public static string Diagnose429(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody))
                return "HTTP 429 with empty body. Likely rate limit or gateway throttling.";

            string body = responseBody.ToLowerInvariant();

            if (body.Contains("insufficient_quota") || body.Contains("billing") || body.Contains("quota"))
                return "HTTP 429 appears to be QUOTA/BILLING related (insufficient quota or billing limit). Check your OpenAI usage/billing.";

            if (body.Contains("rate_limit") || body.Contains("too many requests") || body.Contains("requests per minute") || body.Contains("tokens per minute"))
                return "HTTP 429 appears to be RATE LIMIT related. Reduce request frequency + add exponential backoff.";

            return "HTTP 429 received. Could be rate limit or quota. Check response 'error.type'/'error.code' in the body.";
        }
    }
}
