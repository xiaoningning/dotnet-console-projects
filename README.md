# dotnet-console-projects

It is all based on dotnet core 6+.

- basic dotnet data structure
- serilog: c# logger, which support microsoft.extension.logging
  - serilog in console
  - serilog in host service via DI
- TPL dataflow block based pipeline
- TPL based job queue: async, threads, priority batch block
- priority block collection based dotnet 6+ priorityQueue with a simple lock (slow)
- dotnet XUnit / lib class
- httpclient sync/async post/get/postjson
- c# system.text.json, JsonNode (c# 6+)
- Dependency Injection c#
- Host worker builder
- API rate limiter: 
  - fix window algorithm with concurrency support
  - bucket rate limiter algorithm
