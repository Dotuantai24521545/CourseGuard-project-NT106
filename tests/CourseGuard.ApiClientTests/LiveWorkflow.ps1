param([string]$SessionDirectory='.local/api-test-sessions',[string]$ApiBase='http://127.0.0.1:5087')
$ErrorActionPreference='Stop'

function Session($name){
 $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
 $suffix=if($name -eq 'admin'){''}else{'-'+$name}
 foreach($cookie in (Get-Content (Join-Path $SessionDirectory ('server-e2e-cookies'+$suffix+'.json')) -Raw|ConvertFrom-Json)){$session.Cookies.Add([Uri]$apiBase,[Net.Cookie]::new($cookie.Name,$cookie.Value,$cookie.Path))}
 return $session
}
function Call($session,$path,$body){
 $json=ConvertTo-Json -InputObject $body -Depth 20 -Compress
 $response=Invoke-WebRequest ($apiBase+$path) -Method Post -WebSession $session -Headers @{'X-CourseGuard-Client'='desktop'} -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($json)) -SkipHttpErrorCheck
 if($response.StatusCode -ge 300){throw ($path+' HTTP '+$response.StatusCode)}
 if($response.Content){return ($response.Content|ConvertFrom-Json -Depth 30)}
}
function Check($condition,$name){if(!$condition){throw $name};Write-Output ('PASS '+$name)}
function ConnectWs($path,$session){
 $ws=[Net.WebSockets.ClientWebSocket]::new()
 if($session){$ws.Options.Cookies=$session.Cookies}
 $timeout=[Threading.CancellationTokenSource]::new(10000)
 try{$null=$ws.ConnectAsync([Uri]($ApiBase.Replace('https://','wss://').Replace('http://','ws://')+$path),$timeout.Token).GetAwaiter().GetResult()}finally{$timeout.Dispose()}
 return $ws
}
function ReceiveWs($ws){
 $bytes=[byte[]]::new(65536);$timeout=[Threading.CancellationTokenSource]::new(10000)
 try{$result=$ws.ReceiveAsync([ArraySegment[byte]]::new($bytes),$timeout.Token).GetAwaiter().GetResult();return ,$bytes[0..($result.Count-1)]}finally{$timeout.Dispose()}
}
function PutInt($bytes,$offset,$value){$valueBytes=[BitConverter]::GetBytes([int]$value);[Array]::Reverse($valueBytes);[Array]::Copy($valueBytes,0,$bytes,$offset,4)}
$teacher=Session 'thnghi';$student=Session 'student';$admin=Session 'admin'
$studentProfile=Invoke-RestMethod ($apiBase+'/api/me') -WebSession $student
$course=0
try{
 $name='API_E2E_'+[Guid]::NewGuid().ToString('N')
 $course=Call $teacher '/api/teacher/CreateCourse' @{input=@{name=$name;description='Temporary API integration fixture';status='ACTIVE';startDate=(Get-Date).AddDays(-1).ToString('s');endDate=(Get-Date).AddDays(5).ToString('s')}}
 Check ($course -gt 0) 'teacher creates temporary course'
 $courses=Call $teacher '/api/teacher/GetCourses' @{}
 Check (($courses|Where-Object id -eq $course).status -eq 'DRAFT') 'server prevents teacher bypassing course approval'
 Check (Call $teacher '/api/teacher/SubmitCourseForApproval' @{courseId=$course}) 'teacher submits course approval'
 Check (Call $admin '/api/course/ApproveCourse' @{courseId=$course}) 'admin approves course'
 $null=Call $student '/api/course/RequestEnrollment' @{courseId=$course}
 Check (Call $teacher '/api/teacher/ApproveEnrollment' @{courseId=$course;studentId=$studentProfile.id}) 'teacher approves student enrollment'
 $message=Call $teacher ('/api/courses/'+$course+'/messages') @{content='API E2E hello'}
 $rows=Invoke-RestMethod ($apiBase+'/api/courses/'+$course+'/messages') -WebSession $student
 Check (@($rows|Where-Object id -eq $message.id).Count -eq 1) 'student receives teacher message via server'
 $file=Call $student ('/api/courses/'+$course+'/files') @{fileName='api-test.txt';mimeType='text/plain';caption='API test attachment';bytes=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('API fixture'))}
 $content=Invoke-RestMethod ($apiBase+'/api/courses/'+$course+'/files/'+$file.id) -WebSession $teacher
 Check ($content -eq [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('API fixture'))) 'authorized file upload/download round trip'
 $poll=Call $teacher ('/api/courses/'+$course+'/polls') @{question='API test vote';options=@('A','B')}
 Check ($poll -gt 0) 'teacher creates poll'
 $rows=Invoke-RestMethod ($apiBase+'/api/courses/'+$course+'/messages') -WebSession $student
 $pollRow=$rows|Where-Object {$_.id -eq $poll}|Select-Object -First 1
 $poll=$pollRow.pollId
 Check ($poll -gt 0) 'poll message contains correct poll identifier'
 if($pollRow.poll.options.Count -gt 0){Check (Call $student ('/api/polls/'+$poll+'/vote') @{optionId=$pollRow.poll.options[0].id}) 'student votes through server'}
 Check (Call $teacher ('/api/polls/'+$poll+'/close') @{}) 'teacher closes poll'
 $schedule=Call $teacher '/api/teacher/CreateScheduleItem' @{input=@{courseId=$course;title='API temporary session';startTime=(Get-Date).AddMinutes(-5).ToString('s');endTime=(Get-Date).AddHours(1).ToString('s')}}
 Check ($schedule -gt 0) 'teacher creates classroom session'
 Check (Call $teacher '/api/teacher/UpdateSessionStatusAsync' @{sessionId=$schedule;isOpened=$true;meetingLink=''}) 'teacher opens classroom'
 $ticket=Call $student ('/api/classroom/'+$schedule+'/ticket') @{}
 $teacherSocket=$null;$studentSocket=$null
 try{
  $teacherSocket=ConnectWs ('/api/classroom/stream?classId='+$schedule) $teacher
  $studentSocket=ConnectWs ('/classroom-relay?ticket='+$ticket.ticket) $null
  $signal=@{id='e2e';signal_type='media-state';sender_id='999999';sender_role='teacher';payload=@{mic=$true};receiver_id=$null}|ConvertTo-Json -Depth 5
  $null=$studentSocket.SendAsync([ArraySegment[byte]]::new([Text.Encoding]::UTF8.GetBytes($signal)),[Net.WebSockets.WebSocketMessageType]::Text,$true,[Threading.CancellationToken]::None).GetAwaiter().GetResult()
  $received=([Text.Encoding]::UTF8.GetString([byte[]](ReceiveWs $teacherSocket)))|ConvertFrom-Json
  Check ($received.sender_id -eq [string]$studentProfile.id -and $received.sender_role -eq 'student') 'classroom signaling authenticates participant and replaces forged identity'
  $replayed=$null
  try{$replayed=ConnectWs ('/classroom-relay?ticket='+$ticket.ticket) $null;throw 'Ticket replay unexpectedly accepted'}catch [Net.WebSockets.WebSocketException]{Write-Output 'PASS classroom ticket cannot be replayed'}finally{if($replayed){$replayed.Abort();$replayed.Dispose()}}
 }finally{if($teacherSocket){$teacherSocket.Abort();$teacherSocket.Dispose()};if($studentSocket){$studentSocket.Abort();$studentSocket.Dispose()}}
 $attendance=Call $student ('/api/student/attendance/'+$schedule+'/in') @{}
 Check ($attendance -gt 0) 'student checks in through server'
 Check (Call $student ('/api/student/attendance/'+$attendance+'/out') @{}) 'student checks out through server'
 $assignment=Call $teacher '/api/teacher/CreateAssignment' @{input=@{courseId=$course;title='API temporary assignment';description='Integration fixture';status='OPEN';dueAt=(Get-Date).AddDays(1).ToString('s')}}
 Check ($assignment -gt 0) 'teacher creates assignment'
 Check (Call $student '/api/student/submissions' @{assignmentId=$assignment;studentId=999999;fileName='submission.txt';fileContent=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('Test submission'));contentType='text/plain'}) 'student submits assignment; server ignores forged studentId'
 $exam=Call $teacher '/api/teacher/CreateExam' @{input=@{courseId=$course;title='API temporary exam';openTime=(Get-Date).AddMinutes(-5).ToString('s');closeTime=(Get-Date).AddHours(1).ToString('s');durationMinutes=30;maxAttempts=1;maxViolations=5;status='ACTIVE'}}
 Check ($exam -gt 0) 'teacher creates draft exam'
 $question=Call $teacher '/api/teacher/CreateExamQuestion' @{input=@{examId=$exam;questionText='2 + 2 = ?';optionA='4';optionB='3';optionC='2';optionD='1';correctOption='A';points=10;displayOrder=1;difficulty='EASY';questionType='MULTIPLE_CHOICE'}}
 Check ($question -gt 0) 'teacher adds exam question'
 Check (Call $teacher '/api/teacher/ActivateExam' @{examId=$exam}) 'teacher activates exam'
 $attempt=Call $student '/api/student/StartOrResumeStudentExam' @{examId=$exam}
 Check ($attempt.attemptId -gt 0 -and $attempt.questions.Count -eq 1) 'student starts exam through API with populated question list'
 $teacherSocket=$null;$studentSocket=$null
 try{
  $teacherSocket=ConnectWs ('/api/monitor/stream?examId='+$exam) $teacher
  $studentSocket=ConnectWs ('/api/monitor/stream?examId='+$exam+'&attemptId='+$attempt.attemptId) $student
  $frame=[byte[]]::new(34);$frame[0]=67;$frame[1]=71;$frame[2]=83;$frame[3]=70;$frame[4]=1
  PutInt $frame 6 $exam;PutInt $frame 10 $studentProfile.id;PutInt $frame 14 $attempt.attemptId;PutInt $frame 26 4
  $frame[30]=255;$frame[31]=216;$frame[32]=255;$frame[33]=217
  $null=$studentSocket.SendAsync([ArraySegment[byte]]::new($frame),[Net.WebSockets.WebSocketMessageType]::Binary,$true,[Threading.CancellationToken]::None).GetAwaiter().GetResult()
  $received=ReceiveWs $teacherSocket
  Check ($received.Count -eq 34) 'authenticated monitor forwards synthetic frame only to owning teacher'
 }finally{if($teacherSocket){$teacherSocket.Abort();$teacherSocket.Dispose()};if($studentSocket){$studentSocket.Abort();$studentSocket.Dispose()}}
 Check (Call $student '/api/student/SaveStudentExamAnswer' @{attemptId=$attempt.attemptId;examQuestionId=$attempt.questions[0].id;selectedOption='A'}) 'student saves answer through API'
 $submitted=Call $student '/api/student/SubmitStudentExamAttempt' @{attemptId=$attempt.attemptId}
 Check ($submitted.success -and $submitted.score -eq 10) 'server grades submitted attempt correctly'
}finally{
 if($course -gt 0){
  $current=Call $teacher '/api/teacher/GetCourses' @{}
  if(($current|Where-Object id -eq $course).name -eq $name){Check (Call $admin '/api/course/DeleteCourse' @{courseId=$course}) 'temporary course fixture removed'}
 }
}
