# Amazon Connect ChoiceView Integration Lambda Function Project

This project consists of:
* Function.cs - class file containing a class with a single function handler method
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS
* Files ending with Workflow.cs - These files contain the ChoiceView and Twilio API calls needed to integrate ChoiceView with Amazon Connect contact workflows.

## Here are some steps to follow from Visual Studio:

To deploy your function to AWS Lambda, right click the project in Solution Explorer and select *Publish to AWS Lambda*.

To view your deployed function open its Function View window by double-clicking the function name shown beneath the AWS Lambda node in the AWS Explorer tree.

To perform testing against your deployed function use the Test Invoke tab in the opened Function View window.

To configure event sources for your deployed function, for example to have your function invoked when an object is created in an Amazon S3 bucket, use the Event Sources tab in the opened Function View window.

To update the runtime configuration of your deployed function use the Configuration tab in the opened Function View window.

To view execution logs of invocations of your function use the Logs tab in the opened Function View window.

## Here are some steps to follow to get started from the command line:

Once you have edited your function you can use the following command lines to build, test and deploy your function to AWS Lambda from the command line:

Restore dependencies
```
    cd "ChoiceViewAPI"
    dotnet restore
```

Execute unit tests
```
    cd "ChoiceViewAPI/test/ChoiceViewAPI.Tests"
    dotnet test
```

Deploy function to AWS Lambda
```
    cd "ChoiceViewAPI/src/ChoiceViewAPI"
    dotnet lambda deploy-function
```

Create the deployment package for the Quick Start
```
    dotnet lambda package functions/packages/choiceview-integration/ChoiceViewAPI.zip --project-location functions/source/choiceview-integration/ChoiceViewAPI --config-file aws-lambda-tools-defaults.json --configuration Release 
```

## Additonal deployment steps

When deploying, you must set several environment variables.

You need to have the client id and client secret to this instance of the ChoiceView API, and the base url of the ChoiceView server.

* CHOICEVIEW_CLIENTID - The client id assigned to the function.
* CHOICEVIEW_CLIENTSECRET - The client secret assigned to the function.

You also need the base url of the ChoiceView server that will service the request. Unless you are working with unreleased versions of the ChoiceView API, you should use the production server cvnet.radishsystems.com.

* CHOICEVIEW_SERVICEURL - The base url, either https://cvnet2.radishsystems.com/ivr/api/ or https://cvnet.radishsystems.com/ivr/api/

You also need Twilio API credentials in order to send SMS notifications to mobile clients.

* TWILIO_AUTHTOKEN - The AuthToken assigned to the function.
* TWILIO_ACCOUNTSID - The AccountSid assigned to the function.

Set the allocated memory to 1024 MB for best cold start time.

Set the function timeout to 3 minutes (180 seconds).