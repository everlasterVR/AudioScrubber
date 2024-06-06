#!/bin/bash

set -e

./scripts/generate_cslist.sh
author_name=everlaster
resource_name=AudioScrubber

package_version=$1
[ -z "$package_version" ] && printf "Usage: ./package.sh [var package version]\n" && exit 1

plugin_version=$(git describe --tags --match "v*" --abbrev=0 HEAD 2>/dev/null | sed s/v//)
[ -z "$plugin_version" ] && printf "Git tag not set on current commit.\n" && exit 1

# packaging: main
work_dir=publish
mkdir -p work_dir

resource_dir="$work_dir"/Custom/Scripts/"$author_name"/"$resource_name"
mkdir -p "$resource_dir"
cp meta.json "$work_dir"/
cp ./*.cslist "$resource_dir"/
cp -r src "$resource_dir"/

# update version info
sed -i "s/0\.0\.0/$plugin_version/g" "$work_dir"/meta.json

for file in $(find "$resource_dir" -type f -name "*.cs"); do
    # set production env
    sed -i "s/#define ENV_DEVELOPMENT/\/\//" "$file"
    # hide .cs files (plugin is loaded with .cslist)
    touch "$file".hide
done

# zip files to .var and cleanup
printf "Creating package...\n"
package_file="$author_name"."$resource_name"."$package_version".var
cd "$work_dir"
zip -rq "$package_file" ./*
printf "Package %s created for plugin version v%s.\n" "$package_file" "$plugin_version"
mv "$package_file" ..
cd ..
rm -rf "$work_dir"

# move archive to AddonPackages
addon_packages_dir=../../../../AddonPackages/Self
mkdir -p "$addon_packages_dir"
mv "$package_file" $addon_packages_dir
printf "Package %s moved to AddonPackages/Self.\n" "$package_file"
