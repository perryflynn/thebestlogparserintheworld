# tHE bEST lOG pARSER iN tHE wORLD

A access log parser to create basic statistics from huge log files.

## Run

This tool is developed with .NET Core.

To run it, the .NET Core Runtime Environment is required:

```sh
dotnet run -- --help
```

Or it must build in self-contained mode:

```sh
mkdir dist
dotnet publish -c Release -r linux-x64 --self-contained -o dist/
cd dist
./logsplit --help
```

## State of development

Early alpha, this tool is just for nerds at the moment.

## Quickstart

### Init repository

```sh
mkdir /home/christian/weblogs
logsplit init -d /home/christian/weblogs -f examplewebsite
cd /home/christian/weblogs
```

(See `logsplit init --help` for more info)

### Put logs into import folder

Now you can put all log files from the website
`examplewebsite` into `/home/christian/weblogs/input/examplewebsite`.

Inside of the folder is also a `loginfo.json`, which contains the configuration
for parsing the access logs. This should fit for NGINX logs when the filename
format is something like `example-access.log.1.gz`.

Just edit the JSON file if something is not working.

### Import

The import splits the logfiles into one access log per
host, per hostgroup, per month.

```sh
cd /home/christian/weblogs
logsplit import
```

When something goes wrong while importing, just delete all
`*.new` files in `/home/christian/weblogs/repository` and try again.

(See `logsplit import --help` for more info)

### Analyze

The analyze process parses the log files and generates a summary JSON
file which can be used to generate the actual statistics.

After this process, the raw accesslogs are not needed anymore.

```
cd /home/christian/weblogs
logsplit analyze
```

(See `logsplit analyze --help` for more info)

### Create statistics

This module is in a very early state.

Maybe C# Skills are required to get the infos that you desire.

```sh
logsplit statistic -p "-access_log_examplewebsite-"
```

The `-p` parameter is a regular expression which must match a `.gz.json`
file in the repository folder.
