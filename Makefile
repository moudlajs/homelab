PROJECT = src/HomeLab.Cli/HomeLab.Cli.csproj
TESTS   = src/HomeLab.Cli.Tests/HomeLab.Cli.Tests.csproj
OUT     = publish-single
BIN     = $(HOME)/.local/bin/homelab
RID     = osx-arm64

.PHONY: build test format publish install clean

build:
	dotnet build $(PROJECT)

test:
	dotnet test $(TESTS)

format:
	dotnet format --verify-no-changes

publish:
	dotnet publish $(PROJECT) -c Release -r $(RID) --self-contained true /p:PublishSingleFile=true -o $(OUT)

install: publish
	cp $(OUT)/HomeLab.Cli $(BIN)
	codesign -f -s - $(BIN)
	xattr -cr $(BIN)
	@echo "Installed to $(BIN)"

clean:
	dotnet clean $(PROJECT)
	rm -rf $(OUT)
