# ChoiceView Lambda Function Parameters

## Introduction
This document describes how to use the ChoiceView Lambda to manage a ChoiceView session for sending content to a ChoiceView client.

## Overview
Your contact flow will invoke the ChoiceView Lambda whenever it needs to interact with the ChoiceView client running in the caller's web browser. 

## Common Lambda Parameters
Lambda parameter are key-value pairs. The key is named __Destination Key__, and the value is named __Value__.

All ChoiceView actions must have a RequestName parameter. This parameter specifies the action to be performed by ChoiceView.

These are the currently available actions:
- CreateSession
- CreateSessionWithSms
- QuerySession
- GetSession
- EndSession
- TransferSession
- GetPhoneNumberType
- SendSms
- SendUrl
- GetControlMessage
- ClearControlMessage
- GetProperties
- AddProperty
  
These actions are described in subsequent sections.

## Common Lambda Responses
The Lambda will return parameters to the contact flow after it executes. These parameters indicate whether the execution succeeded or failed, and returns information requested from the ChoiceView server or information entered into a web page loaded into the ChoiceView client. 
This information usually needs to be copied from the Lambda response to contact flow variables for further processing. 
_If the response parameters for the CreateSession, CreateSessionWithSms, and QuerySession actions are not copied by the contact flow to the contact flow variables, subsequent lambda calls will probably fail._

The response parameters common to all actions:
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.

If **LambdaResult** is _False_, the Lambda encountered a problem that prevents it from connecting to the ChoiceView service. Check the CloudFront log for the Lambda for more information on the failure.

## ChoiceView Actions
This section describes the actions that can be requested from the Lambda function and what parameters to pass to the Lambda function to perform each action.

### CreateSessionWithSms Action
This action attempts to create a ChoiceView session for a caller using a mobile phone equipped with a web browser.

If Connect did not return the caller's phone number, then no attempt can be made to start a ChoiceView session, and the Lambda function request returns an error.

This action only creates a ChoiceView session for the caller. The caller must start the ChoiceView session by loading the ChoiceView client using the url in the text message.

This action is the fastest way to create a ChoiceView session and send the caller the url for starting the session. 
If the caller does not have a number that can receive texts, or you want your workflow to have more control over the session startup, you can start the session in other ways.

To start a ChoiceView session, the following steps are taken:

1.  __Check phone number type.__
    A check is made to determine if the caller is calling from a phone number that can receive text messages.

2.  __Send text message with link to ChoiceView client.__
    If the caller's phone number can receive text messages, a text message with a link to the ChoiceView client is sent to the caller.
    If the caller's phone cannot receive text messages, or the lambda cannot send the text message, the function returns an error.

3.  __Sends a request to the ChoiceView servers to start the session.__
    The request does not start the session. For the session to start, the caller must start a ChoiceView client using the same phone number that was used to call the Connect instance. 
    The url sent in the text message will automatically start the client with the caller's phone number when clicked.

If the Choiceview session create request is successfully sent to the ChoiceView server, the Lambda returns a success indication.

After requesting the session start, the caller has approximately 2 minutes to start the ChoiceView client. If the client does not connect to the ChoiceView server after 2 minutes, the server ends the session.
The contact flow must check the session status to determine when the caller has connected to the server. This is done by calling the **QuerySession** action.

#### CreateSessionWithSms Parameters
The Lambda needs the following parameters to execute the CreateSessionWithSms action:
- **RequestName**: CreateSessionWithSms
- **SmsMessage**: An optional string with the text message to be sent to the caller

If the text message does not contain a url for starting the ChoiceView client, the text message content will have this string appended:
> Tap this link to start ChoiceView: <https://choiceview.com/secure.html?phone=><caller's phone number>

This is the default url for starting the ChoiceView client. If the SmsMessage parameter is not set, then this is the text message that is sent to the caller.

#### CreateSessionWithSms Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
- **SmsSent**
  _True_ if a text message was sent to the caller's phone.
- **ConnectStartTime**
  The time that the ChoiceView session was created. The caller has 2 minutes to start the ChoiceView session in her browser. 
  _This response must be copied to the contact attributes for the **QuerySession** action to work properly._
- **QueryUrl**
  The url used by the QuerySession action to check the state of the ChoiceView session.
  _This response must be copied to the contact attributes for the **QuerySession** action to work properly._
- **SessionRetrieved**
  _True_ if the session state is now available. This indicates that the caller has started the session.
  If **SessionRetrieved** is true, then **ConnectStartTime** and **QueryUrl** will not be returned, and do not have to be copied to the contact attributes.
- **SessionUrl**
  This url is called by the lambda to perform session operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 
- **PropertiesUrl**
  This url is called by the lambda to perform session property operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 
- **ControlMessageUrl**
  This url is called by the lambda to perform control message operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 

When **SessionRetrieved** is _True_, the response will also contain the current state of the session and the session properties. 
See the **GetSession** action for details about the session state and session parameters.
 
This action will execute the **GetPhoneNumberType** action. If the caller's phone number is not of type "mobile", the action may succeed, but **SmsSent** will be _False_.

This action usually returns the result of sending the SMS message, the QueryUrl for checking the session state and the time that the session was created. 
These responses have to be copied to the contact attributes to be referenced by the **QuerySession** action.

It is possible that the caller created the ChoiceView session before the **CreateSessionWithSms** action is called. 
In that case, there will be no **QueryUrl** and **ConnectStartTime** response parameters, and there will be a **SessionRetrieved** response parameter. 
In that case, the response should be handled as if it came from a **QuerySession** action. 
    
### QuerySession Action
This action checks to see if the caller has started the ChoiceView session. The function returns success if the session has started, or a failure if the session has not yet started, or has ended due to timeout or failure.

This action is designed to be executed in a loop until the caller has started the ChoiceView session. The contact flow can be written to play an announcement during this loop prompting the caller to check for a text message and click on the link in the text message.

_The CreateSessionWithSms or CreateSession actions must have completed successfully, and the responses from those actions must have been saved to the contact attributes for this action to execute successfully._

#### QuerySession Parameters
The Lambda needs the following parameters to execute the QuerySession action:
- **RequestName**: QuerySession

#### QuerySession Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
- **SessionTimeout**
  _True_ if the ChoiceView session has timed out while waiting for the caller to connect.
  _Subsequent calls to QuerySession will fail when the session times out._ A new session has to be started with CreateSession or CreateSessionWithSms.
- **SessionRetrieved**
  _True_ if the session state is now available. This indicates that the caller has started the session.
- **SessionUrl**
  This url is called by the lambda to perform session operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 
- **PropertiesUrl**
  This url is called by the lambda to perform session property operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 
- **ControlMessageUrl**
  This url is called by the lambda to perform control message operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 

When **SessionRetrieved** is _True_, the response will also contain the current state of the session and the session properties. 
See the **GetSession** action for details about the session state and session parameters.

### CreateSession Action
This action attempts to create a ChoiceView session for a caller using a mobile phone equipped with a web browser.
However, it does not send a text message to the caller with the ChoiceView client url.  This action cana be used to provide a more customized session creation experience to the caller. 
The Lambda exposes all of the actions needed to send a text message to the caller, so the contact flow can provide more information to the caller during session creation. 
For example, the caller can be asked if she wants to receive a text message, or told to start the client from a shortcut or link in the caller's browser.

#### CreateSession Parameters
The Lambda needs the following parameters to execute the CreateSession action:
- **RequestName**: CreateSession

#### CreateSession Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
- **ConnectStartTime**
  The time that the ChoiceView session was created. The caller has 2 minutes to start the ChoiceView session in her browser.
- **QueryUrl**
  The url used by the **QuerySession** action to check the state of the ChoiceView session.
- **SessionRetrieved**
  _True_ if the session state is now available. This indicates that the caller has started the session.
  If **SessionRetrieved** is _True, then **ConnectStartTime** and **QueryUrl** will not be returned, and do not have to be copied to the contact attributes.
- **SessionUrl**
  This url is called by the lambda to perform session operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 
- **PropertiesUrl**
  This url is called by the lambda to perform session property operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to successfully execute subsequent actions. 
- **ControlMessageUrl**
  This url is called by the lambda to perform control message operations. It is returned when the **SessionRetrieved** parameter is _True_. 
  This parameter must be copied to the contact attributes for the lambda to sucessfully execute subsequent actions. 

When **SessionRetrieved** is _True_, the response will also contain the current state of the session and the session properties. 
See the **GetSession** action for details about the session state and session parameters.
 
This action usually returns the QueryUrl for checking the session state and the time that the session was created. 
These responses have to be copied to the contact attributes to be referenced by the **QuerySession** action.

It is possible that the caller created the ChoiceView session before the **CreateSession** action is called. 
In that case, there will be no **QueryUrl** and **ConnectStartTime** response parameters, and there will be a **SessionRetrieved** response parameter. 
In that case, the response should be handled as if it came from a **QuerySession** action. 

### GetPhoneNumberType Action
This action attempts to determine if the caller's phone number can accept SMS messages. It uses the Twilio API to lookup this information. A number is identified as either mobile, landline, or VOIP.

Note that this action is not 100% accurate. We have seen cases where the number has been misidentified.

The **SendSms** and **CreateSessionWithSms** actions execute this action before sending the SMS message.
   
#### GetPhoneNumberType Parameters
The Lambda needs the following parameters to execute the GetPhoneNumberType action
- **RequestName**: GetPhoneNumberType

#### GetPhoneNumberType Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
- **NumberType**
  Is "mobile" if the number references a mobile phone that presumably can receive a SMS message.
  
### SendSms Action
This action attempts to send an SMS message to the caller's phone. It uses the Twilio API to lookup this information. 
The SMS message contains a url that starts the ChoiceView session when loaded into the caller's web browser.

This action will execute the **GetPhoneNumberType** action. If the caller's phone number is not of type "mobile", the action will fail.
   
#### SendSms Parameters
The Lambda needs the following parameters to execute the SendSms action
- **RequestName**: SendSms
- **SmsMessage**: A string with the text message to be sent to the caller

#### SendSms Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.

### GetSession Action
This action gets the current state of the ChoiceView session from the server. It also returns ChoiceView session properties. 
The properties are key value pairs that contain additional information about this session. These properties are used by the ChoiceView server and live agents. 
The lambda can use session properties to pass information to the server or live agents participating in this call. Contact Radish for more information about the session properties.   

#### GetSession Parameters
The Lambda needs the following parameters to execute the GetSession action:
- **RequestName**: GetSession

#### GetSession Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
- **SessionStatus**
  The state of the ChoiceView session. This can be 'connected', 'disconnected', or empty. 
  If it is 'connected', then the session is connected to the caller's web browser. If it is 'disconnected', the session is over.
  Otherwise there is a problem with the network or the lambda configuration.

There will be additional response parameters corresponding to the ChoiceView session properties. These are used for advanced call scenarios that require interaction with the ChoiceView Server or live agents.

### EndSession Action
This action ends the ChoiceView session. It should be called when your contact flows no longer need a ChoiceView session with the caller, or the voice call has ended. 
Failure to end the session at the end of the voice call will result in the session staying active until the caller ends the session or the session eventually times out.
   
#### EndSession Parameters
The Lambda needs the following parameters to execute the EndSession action:
- **RequestName**: EndSession

#### EndSession Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
 
### SendUrl Action
This action sends a url to the caller's ChoiceView client. The url's content is displayed to the caller.
There is no restriction on the type of url. Any content acceptable by the caller's web browser can be provided.
The url can refer to a ChoiceView form, which submits the form data to the ChoiceView server for processing.
   
#### SendUrl Parameters
The Lambda needs the following parameters to execute the SendUrl action:
- **RequestName**: SendUrl
- **ClientUrl**: The url to send to the caller.

#### SendUrl Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.

### GetControlMessage Action
This action checks for form data from a ChoiceView form. If form data is available, it is returned as response parameters. This action does not remove the form data from the ChoiceView server after reading.
Call the **ClearControlMessage** action to remove form data after retrieval.
   
#### GetControlMessage Parameters
The Lambda needs the following parameters to execute the GetControlMessage action:
- **RequestName**: GetControlMessage

#### GetControlMessage Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
- **ControlMessageAvailable**
  If ControlMessageAvailable is _True_, data has been received from a ChoiceView form.
  
If **ControlMessageAvailable** is true, the form data is returned as response parameters.
The parameter name is the name of the form data field, the parameter value is the value contained in the form data field.

### ClearControlMessage Action
This action removes all received form data.  
   
#### ClearControlMessage Parameters
The Lambda needs the following parameters to execute the ClearControlMessage action:
- **RequestName**: ClearControlMessage

#### ClearControlMessage Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.

### GetProperties Action
This action returns the session properties. It is very similar to the **GetSession** action.
If this action returns success, you can assume that the ChoiceView session state is 'connected'.
  
#### GetProperties Parameters
The Lambda needs the following parameters to execute the GetProperties action:
- **RequestName**: GetProperties

#### GetProperties Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.

There will be additional response parameters corresponding to the ChoiceView session properties. 
These are used for advanced call scenarios that require interaction with the ChoiceView Server or live agents.

### AddProperty Action
This action adds a session property to the ChoiceView session. This property will be accessible by the ChoiceView server and any ChoiceView agents that are connected to the session. 
This action can be used to pass data to the server and the agents.
   
#### AddProperty Parameters
The Lambda needs the following parameters to execute the AddProperty action:
- **RequestName**: AddProperties
- **PropertyName**: The name of the ChoiceView session property to add.
- **PropertyValue**: The value of the added ChoiceView session property.

#### AddProperty Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.

### TransferSession Action
This action transfers the ChoiceView session to an agent. The agent must be logged into the ChoiceView Agent software, and logged into Amazon Connect. The contact flow during the transfer must also transfer the voice call to the agent. 
Contact Radish Systems for more information on using ChoiceView agents.
   
#### TransferSession Parameters
The Lambda needs the following parameters to execute the TransferSession action:
- **RequestName**: TransferSession
- **AccountId**: The ChoiceView account id that will receive the ChoiceView session.

#### TransferSession Responses
- **LambdaResult** 
  _True_ if the Lambda action executed successfully.
- **FailureReason**
  If **LambdaResult** is _False_, this parameter contains a string describing the reason for the failure.
   