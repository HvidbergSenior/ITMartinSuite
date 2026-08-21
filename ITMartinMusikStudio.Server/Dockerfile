FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore ITMartinMusikStudio.Server/ITMartinMusikStudio.Server.csproj

RUN dotnet publish ITMartinMusikStudio.Server/ITMartinMusikStudio.Server.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

RUN apt-get update && \
    apt-get install -y ffmpeg python3 python3-pip \
    && rm -rf /var/lib/apt/lists/*

# Stems (demucs) and note-level transcription (piano_transcription_inference)
# for Studio's "Udtræk spor" / "Node-transskription" cards - CPU-only torch,
# no GPU in this container. --break-system-packages needed on Debian
# bookworm's externally-managed Python (no venv here, single-purpose image).
RUN pip3 install --break-system-packages --no-cache-dir \
    torch torchaudio --index-url https://download.pytorch.org/whl/cpu --extra-index-url https://pypi.org/simple && \
    pip3 install --break-system-packages --no-cache-dir \
    demucs piano_transcription_inference pretty_midi soundfile librosa

# Bake in the ~165MB piano-transcription checkpoint at build time (source:
# Zenodo, the model authors' own release - see PianoTranscriptionService.cs)
# so first real use doesn't need a runtime download or `wget` (not present
# in this image either).
RUN mkdir -p /root/piano_transcription_inference_data && \
    python3 -c "import urllib.request; urllib.request.urlretrieve('https://zenodo.org/record/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth?download=1', '/root/piano_transcription_inference_data/note_F1=0.9677_pedal_F1=0.9186.pth')"

COPY --from=build /app/publish .

RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "ITMartinMusikStudio.Server.dll"]
