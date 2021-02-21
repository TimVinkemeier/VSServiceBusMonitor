# Azure ServiceBus Monitor for Visual Studio

Adds a configurable status bar entry to show current runtime stats of monitored Azure ServiceBus entities (queues and subscriptions).
Currently retrieves and regularly updates the active and deadletter message counts.

Download the extension from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=TimVinkemeier.vsservicebusmonitor).

--------------------------------------

## Features

- Monitor Azure ServiceBus queues and topics
- Show active and deadletter message counts
- Fully configurable - monitor any number of entities and show only those that interest you
- Supports multiple profiles within one configuration
- Configurable to show entities only when a deadletter message is present

### Status Bar indicator

The configured ServiceBus entities are shown in the status bar with their active and deadletter message counts:

![Extension in status bar](images/status-bar-example-01.png)

## Getting Started

1. Install the extension.
2. Open a solution.
3. If no configuration file is found (the extension looks for `.vs\service-bus-monitor.config.json`), an info bar will be shown within solution explorer.

![Info Bar in Solution Explorer](images/info-bar.png)

4. Click the "Add empty configuration" link in the info bar.
5. A new configuration is opened with one example profile.
6. Provide a valid Azure Service Bus connection string (with Manage rights) and at least one queue or subscription definition.
7. Save the file - the extension automatically reloads the configuration and updates the status bar.

:information_source: The configuration file is located within the `.vs` folder since that is normally ignored from source control. Since the service bus connection string is a sensitive value, keep it secret.

## Configuration

TODO
