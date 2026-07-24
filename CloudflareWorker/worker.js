/**
 * Movie Manager Desktop - CORS Proxy Worker
 * 
 * Deploy this script to Cloudflare Workers to act as a proxy
 * that bypasses local internet restrictions.
 * 
 * Usage: https://your-worker-name.your-subdomain.workers.dev/?url=https://api.tvmaze.com/search
 */

export default {
    async fetch(request, env, ctx) {
      // Handle CORS preflight requests
      if (request.method === "OPTIONS") {
        return new Response(null, {
          headers: {
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Methods": "GET, POST, PUT, DELETE, OPTIONS",
            "Access-Control-Allow-Headers": request.headers.get("Access-Control-Request-Headers") || "*",
          },
        });
      }
  
      const url = new URL(request.url);
      const targetUrl = url.searchParams.get("url");
  
      if (!targetUrl) {
        return new Response("Missing 'url' query parameter.", { status: 400 });
      }
  
      try {
        // Clone the original request headers
        const headers = new Headers(request.headers);
        // Remove headers that might cause issues
        headers.delete("Host");
        headers.delete("Origin");
        headers.delete("Referer");
  
        // Fetch from the target URL
        const modifiedRequest = new Request(targetUrl, {
          method: request.method,
          headers: headers,
          body: request.body,
          redirect: "follow",
        });
  
        const response = await fetch(modifiedRequest);
        
        // Clone the response so we can modify headers
        const modifiedResponse = new Response(response.body, response);
  
        // Add CORS headers to the response
        modifiedResponse.headers.set("Access-Control-Allow-Origin", "*");
        
        // Remove security headers that might prevent framing or loading
        modifiedResponse.headers.delete("X-Frame-Options");
        modifiedResponse.headers.delete("Content-Security-Policy");
  
        return modifiedResponse;
      } catch (error) {
        return new Response(`Proxy Error: ${error.message}`, { status: 500 });
      }
    },
  };
