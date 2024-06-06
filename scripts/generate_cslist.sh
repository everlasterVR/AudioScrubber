#!/bin/bash

files=$(grep -o '<Compile Include="[^"]*"' AudioScrubber.csproj | sed 's/<Compile Include="//; s/"//')
echo "$files" > AudioScrubber.cslist
