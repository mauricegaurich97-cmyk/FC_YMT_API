# ── Simple Runtime-Only Container ──
# No build step needed - just copy the pre-built publish output
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY publish-linux/ .

EXPOSE 8080

ENTRYPOINT ["dotnet", "FC_YMT_API.dll"]
