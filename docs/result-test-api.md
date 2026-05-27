http://localhost:5049/api/v1/lab-assignments/fd59bef7-dfe3-4b7e-bbf9-8e401006dabe/testcases
{
"status": true,
"message": "Success",
"data": [
{
"id": "f4ba0c35-e136-4a66-8e79-7a9a33a3c8e7",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "SOURCE",
"urlTemplate": "project-count:3",
"description": "Solution has 3 .csproj files (3-layer architecture)",
"inputJson": null,
"expectJson": null,
"expectedStatusCode": 200,
"matchMode": "StatusOnly",
"score": 0.5,
"status": "Approved",
"aiGenerated": false,
"order": 1,
"createdAt": "2026-05-27T13:59:01.445449Z",
"updatedAt": "2026-05-27T14:01:08.450757Z"
},
{
"id": "829200ea-81fa-4dd5-a3d6-01dd4f6458a8",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "SOURCE",
"urlTemplate": "file-exists:**/docker-compose.yml",
"description": "docker-compose.yml present in submission",
"inputJson": null,
"expectJson": null,
"expectedStatusCode": 200,
"matchMode": "StatusOnly",
"score": 0.5,
"status": "Approved",
"aiGenerated": false,
"order": 2,
"createdAt": "2026-05-27T13:59:01.46217Z",
"updatedAt": "2026-05-27T14:01:08.450749Z"
},
{
"id": "e2c47391-d1bd-4467-9293-2054d8694884",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "SOURCE",
"urlTemplate": "project-name:PRN232.\*.API",
"description": "Solution matches naming convention PRN232.[ProjectName].API",
"inputJson": null,
"expectJson": null,
"expectedStatusCode": 200,
"matchMode": "StatusOnly",
"score": 0.5,
"status": "Approved",
"aiGenerated": false,
"order": 3,
"createdAt": "2026-05-27T13:59:01.462232Z",
"updatedAt": "2026-05-27T14:01:08.450757Z"
},
{
"id": "29c8a1e4-5011-4930-b1e0-f56acc5f924b",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "SOURCE",
"urlTemplate": "file-exists:**/Dockerfile",
"description": "Dockerfile present for containerizing the API application",
"inputJson": null,
"expectJson": null,
"expectedStatusCode": 200,
"matchMode": "StatusOnly",
"score": 0.5,
"status": "Approved",
"aiGenerated": false,
"order": 4,
"createdAt": "2026-05-27T13:59:01.462269Z",
"updatedAt": "2026-05-27T14:01:08.450747Z"
},
{
"id": "9069fbed-1fb7-4076-9c71-e91322f3e554",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "GET",
"urlTemplate": "/api/students?search=nguyen&sort=fullName,-dateOfBirth&page=2&size=10&fields=studentId,fullName,email",
"description": "GET student collection with mandatory search, sort, paging, and field selection parameters",
"inputJson": null,
"expectJson": {
"success": true
},
"expectedStatusCode": 200,
"matchMode": "Subset",
"score": 1.5,
"status": "Approved",
"aiGenerated": false,
"order": 5,
"createdAt": "2026-05-27T13:59:01.462301Z",
"updatedAt": "2026-05-27T14:01:08.450749Z"
},
{
"id": "a76a22fd-f0ba-427a-ade3-0eb194edaa95",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "GET",
"urlTemplate": "/api/students/1",
"description": "GET single student details by explicit seed ID",
"inputJson": null,
"expectJson": {
"success": true
},
"expectedStatusCode": 200,
"matchMode": "Subset",
"score": 0.5,
"status": "Approved",
"aiGenerated": false,
"order": 6,
"createdAt": "2026-05-27T13:59:01.46232Z",
"updatedAt": "2026-05-27T14:01:08.45075Z"
},
{
"id": "5fc1d2fc-24cc-4607-bc3d-a391fdec62ab",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "GET",
"urlTemplate": "/api/enrollments?search=active&sort=-enrollDate&page=1&size=20&fields=enrollmentId,status&expand=student,course",
"description": "GET enrollment list ensuring search, sort, pagination, selection, and expansion function simultaneously",
"inputJson": null,
"expectJson": {
"success": true
},
"expectedStatusCode": 200,
"matchMode": "Subset",
"score": 1.5,
"status": "Approved",
"aiGenerated": false,
"order": 7,
"createdAt": "2026-05-27T13:59:01.462338Z",
"updatedAt": "2026-05-27T14:01:08.450749Z"
},
{
"id": "b523c6ba-2361-43f5-b37e-4191b068a205",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "POST",
"urlTemplate": "/api/enrollments",
"description": "POST create a new enrollment entity mapping to business/request definitions",
"inputJson": {
"studentId": 1,
"courseId": 1,
"enrollDate": "2026-05-27T10:00:00Z",
"status": "Active"
},
"expectJson": {
"success": true
},
"expectedStatusCode": 201,
"matchMode": "Subset",
"score": 1.5,
"status": "Approved",
"aiGenerated": false,
"order": 8,
"createdAt": "2026-05-27T13:59:01.462353Z",
"updatedAt": "2026-05-27T14:01:08.45075Z"
},
{
"id": "8c286f0e-e414-44c1-a683-3aa9cf24cfeb",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "GET",
"urlTemplate": "/api/courses?page=1&size=10",
"description": "GET list of courses including dynamic page metadata values",
"inputJson": null,
"expectJson": {
"success": true
},
"expectedStatusCode": 200,
"matchMode": "Subset",
"score": 1,
"status": "Approved",
"aiGenerated": false,
"order": 9,
"createdAt": "2026-05-27T13:59:01.462378Z",
"updatedAt": "2026-05-27T14:01:08.450749Z"
},
{
"id": "452d7bd1-c507-41ba-809d-130b5434cfcc",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "POST",
"urlTemplate": "/api/courses",
"description": "POST create a new course structure attached to a specific semester ID",
"inputJson": {
"courseName": "Advanced REST Architecture",
"semesterId": 1
},
"expectJson": {
"success": true
},
"expectedStatusCode": 201,
"matchMode": "Subset",
"score": 1,
"status": "Approved",
"aiGenerated": false,
"order": 10,
"createdAt": "2026-05-27T13:59:01.462395Z",
"updatedAt": "2026-05-27T14:01:08.450748Z"
},
{
"id": "2c7d0193-ad1d-496b-ab78-ebc28875b11c",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "GET",
"urlTemplate": "/api/subjects?page=1&size=10",
"description": "GET collection of curricular subjects using uniform response structure",
"inputJson": null,
"expectJson": {
"success": true
},
"expectedStatusCode": 200,
"matchMode": "Subset",
"score": 0.5,
"status": "Approved",
"aiGenerated": false,
"order": 11,
"createdAt": "2026-05-27T13:59:01.462416Z",
"updatedAt": "2026-05-27T14:01:08.450748Z"
},
{
"id": "35987d5a-cdec-4d86-b784-4829f97e6676",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"httpMethod": "GET",
"urlTemplate": "/api/semesters?page=1&size=5",
"description": "GET list of academic planning semesters with generic data format verification",
"inputJson": null,
"expectJson": {
"success": true
},
"expectedStatusCode": 200,
"matchMode": "Subset",
"score": 0.5,
"status": "Approved",
"aiGenerated": false,
"order": 12,
"createdAt": "2026-05-27T13:59:01.46243Z",
"updatedAt": "2026-05-27T14:01:08.450748Z"
}
],
"errors": null,
"traceId": null
}

http://localhost:5049/api/v1/lab-submissions?assignmentId=fd59bef7-dfe3-4b7e-bbf9-8e401006dabe
{
"status": true,
"message": "Success",
"data": [
{
"id": "0cd40760-b84b-45b0-9e06-1c294f76e9a8",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"studentCode": "SE180026",
"originalFileName": "SE180026_NgoThanhDat.zip",
"status": "Error",
"createdAt": "2026-05-27T14:02:07.264603Z",
"updatedAt": "2026-05-27T14:10:43.037776Z"
},
{
"id": "39bedb86-2c7a-491a-8122-b57f5459cc20",
"labAssignmentId": "fd59bef7-dfe3-4b7e-bbf9-8e401006dabe",
"studentCode": "SE180234",
"originalFileName": "SE180234_NguyenHuuAnh.zip",
"status": "BuildFailed",
"createdAt": "2026-05-27T14:02:07.251966Z",
"updatedAt": "2026-05-27T14:43:25.288335Z"
}
],
"errors": null,
"traceId": null
}

http://localhost:5049/api/v1/lab-submissions/0cd40760-b84b-45b0-9e06-1c294f76e9a8/results
{
"status": true,
"message": "Success",
"data": {
"submissionId": "0cd40760-b84b-45b0-9e06-1c294f76e9a8",
"studentCode": "SE180026",
"submissionStatus": "Done",
"latestJobId": "239c0624-4cd9-4dde-bd15-ab1f0713800e",
"jobStatus": "Done",
"totalScore": 10,
"results": [
{
"id": "8e90a4b6-796a-4e9a-ab85-5ce90a48b43c",
"labTestCaseId": "f4ba0c35-e136-4a66-8e79-7a9a33a3c8e7",
"httpMethod": "SOURCE",
"urlTemplate": "project-count:3",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found 3 project(s): PRN232.LMS.Services.csproj, PRN232.LMS.Repositories.csproj, PRN232.LMS.API.csproj",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "c5863a20-1b96-472f-b4c2-b6e6926cb059",
"labTestCaseId": "829200ea-81fa-4dd5-a3d6-01dd4f6458a8",
"httpMethod": "SOURCE",
"urlTemplate": "file-exists:**/docker-compose.yml",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found: SE180234_NguyenHuuAnh/docker-compose.yml",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "f9ccceda-685a-4669-a057-1b8b04b33bbd",
"labTestCaseId": "e2c47391-d1bd-4467-9293-2054d8694884",
"httpMethod": "SOURCE",
"urlTemplate": "project-name:PRN232.\*.API",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found: PRN232.LMS.API.csproj",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "fd0b11cc-1b62-48a7-b2a7-3590b9bfda85",
"labTestCaseId": "29c8a1e4-5011-4930-b1e0-f56acc5f924b",
"httpMethod": "SOURCE",
"urlTemplate": "file-exists:**/Dockerfile",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found: SE180234_NguyenHuuAnh/PRN232.LMS.API/Dockerfile",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "1e08e429-6cea-49bd-9611-de854701726b",
"labTestCaseId": "9069fbed-1fb7-4076-9c71-e91322f3e554",
"httpMethod": "GET",
"urlTemplate": "/api/students?search=nguyen&sort=fullName,-dateOfBirth&page=2&size=10&fields=studentId,fullName,email",
"passed": true,
"awardedScore": 1.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[],\"pagination\":{\"page\":2,\"pageSize\":10,\"totalItems\":5,\"totalPages\":1}},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "a3ca8e0b-160a-48d5-b04f-bc0d30c83d28",
"labTestCaseId": "a76a22fd-f0ba-427a-ade3-0eb194edaa95",
"httpMethod": "GET",
"urlTemplate": "/api/students/1",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"studentId\":1,\"fullName\":\"Nguyen Van An\",\"email\":\"student01@fpt.edu.vn\",\"dateOfBirth\":\"2002-02-02T00:00:00\",\"enrollmentCount\":12,\"enrollments\":[{\"enrollmentId\":23,\"studentId\":1,\"courseId\":1,\"enrollDate\":\"2023-01-11T00:00:00\",\"status\":\"Active\",\"student\":null,\"course\":{\"courseId\":1,\"courseName\":\"PRN231 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":{\"semesterId\":1,\"semesterName\":\"Spring 2023\",\"startDate\":\"2023-01-10T00:00:00\",\"endDate\":\"2023-05-20T00:00:00\",\"courseCount\":0,\"courses\":null},\"enrollments\":null}},{\"enrollmentId\":30,\"studentId\":1,\"courseId\":2,\"enrollDate\":\"2023-11-27T00:00:00\",\"status\":\"Pending\",\"student\":null,\"course\":{\"courseId\":2,\"courseName\":\"DBI202 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":{\"semesterId\":1,\"semesterName\":\"Spring 2023\",\"startDate\":\"2023-01-10T00:00:00\",\"endDate\":\"2023-05-20T00:00:00\",\"courseCount\":0,\"courses\":null},\"enrollments\":null}},{\"enrollmentId\":80,\"st",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "3c2c6ef6-9370-4127-943b-d66abae08064",
"labTestCaseId": "5fc1d2fc-24cc-4607-bc3d-a391fdec62ab",
"httpMethod": "GET",
"urlTemplate": "/api/enrollments?search=active&sort=-enrollDate&page=1&size=20&fields=enrollmentId,status&expand=student,course",
"passed": true,
"awardedScore": 1.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"enrollmentId\":373,\"status\":\"Active\"},{\"enrollmentId\":294,\"status\":\"Active\"},{\"enrollmentId\":332,\"status\":\"Active\"},{\"enrollmentId\":153,\"status\":\"Active\"},{\"enrollmentId\":339,\"status\":\"Active\"},{\"enrollmentId\":128,\"status\":\"Active\"},{\"enrollmentId\":191,\"status\":\"Active\"},{\"enrollmentId\":77,\"status\":\"Active\"},{\"enrollmentId\":480,\"status\":\"Active\"},{\"enrollmentId\":446,\"status\":\"Active\"},{\"enrollmentId\":250,\"status\":\"Active\"},{\"enrollmentId\":123,\"status\":\"Active\"},{\"enrollmentId\":395,\"status\":\"Active\"},{\"enrollmentId\":305,\"status\":\"Active\"},{\"enrollmentId\":159,\"status\":\"Active\"},{\"enrollmentId\":174,\"status\":\"Active\"},{\"enrollmentId\":138,\"status\":\"Active\"},{\"enrollmentId\":335,\"status\":\"Active\"},{\"enrollmentId\":162,\"status\":\"Active\"},{\"enrollmentId\":253,\"status\":\"Active\"}],\"pagination\":{\"page\":1,\"pageSize\":20,\"totalItems\":149,\"totalPages\":8}},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "9ffdb0a3-80cd-466a-a1d6-7b5e02657e37",
"labTestCaseId": "b523c6ba-2361-43f5-b37e-4191b068a205",
"httpMethod": "POST",
"urlTemplate": "/api/enrollments",
"passed": true,
"awardedScore": 1.5,
"actualStatusCode": 201,
"actualResponse": "{\"success\":true,\"message\":\"Resource created successfully\",\"data\":{\"enrollmentId\":501,\"studentId\":1,\"courseId\":1,\"enrollDate\":\"2026-05-27T10:00:00Z\",\"status\":\"Active\",\"student\":null,\"course\":null},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "5560ea0b-dac9-48ae-b2ca-6866e21ed832",
"labTestCaseId": "8c286f0e-e414-44c1-a683-3aa9cf24cfeb",
"httpMethod": "GET",
"urlTemplate": "/api/courses?page=1&size=10",
"passed": true,
"awardedScore": 1,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"courseId\":1,\"courseName\":\"PRN231 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":2,\"courseName\":\"DBI202 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":3,\"courseName\":\"MAE101 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":4,\"courseName\":\"SWE201 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":5,\"courseName\":\"PRN232 - Summer 2023\",\"semesterId\":2,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":6,\"courseName\":\"SWD392 - Summer 2023\",\"semesterId\":2,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":7,\"courseName\":\"NWC202 - Summer 2023\",\"semesterId\":2,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":8,\"courseName\":\"CEA201 - Summer 2023\",\"semesterId\":2,\"enrollmentC",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "ea937a14-ab2c-48ca-ab50-72aa9dad5287",
"labTestCaseId": "452d7bd1-c507-41ba-809d-130b5434cfcc",
"httpMethod": "POST",
"urlTemplate": "/api/courses",
"passed": true,
"awardedScore": 1,
"actualStatusCode": 201,
"actualResponse": "{\"success\":true,\"message\":\"Resource created successfully\",\"data\":{\"courseId\":21,\"courseName\":\"Advanced REST Architecture\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "1254e930-33e6-483a-8b56-ad3dfce22b35",
"labTestCaseId": "2c7d0193-ad1d-496b-ab78-ebc28875b11c",
"httpMethod": "GET",
"urlTemplate": "/api/subjects?page=1&size=10",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"subjectId\":1,\"subjectCode\":\"PRN231\",\"subjectName\":\"Application Development using .NET\",\"credit\":3},{\"subjectId\":2,\"subjectCode\":\"PRN232\",\"subjectName\":\"Advanced Cross-Platform Application\",\"credit\":3},{\"subjectId\":3,\"subjectCode\":\"SWD392\",\"subjectName\":\"Software Architecture and Design\",\"credit\":3},{\"subjectId\":4,\"subjectCode\":\"PRJ301\",\"subjectName\":\"Java Web Application Development\",\"credit\":3},{\"subjectId\":5,\"subjectCode\":\"DBI202\",\"subjectName\":\"Introduction to Database\",\"credit\":3},{\"subjectId\":6,\"subjectCode\":\"MAE101\",\"subjectName\":\"Mathematics for Engineering\",\"credit\":3},{\"subjectId\":7,\"subjectCode\":\"CEA201\",\"subjectName\":\"Computer Organization and Assembly\",\"credit\":3},{\"subjectId\":8,\"subjectCode\":\"NWC202\",\"subjectName\":\"Computer Networking\",\"credit\":3},{\"subjectId\":9,\"subjectCode\":\"SWE201\",\"subjectName\":\"Introduction to Software Engineering\",\"credit\":3},{\"subjectId\":10,\"subjectCode\":\"ITE302\",\"subjectN",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "374623e3-5312-4981-8523-05b27ae03232",
"labTestCaseId": "35987d5a-cdec-4d86-b784-4829f97e6676",
"httpMethod": "GET",
"urlTemplate": "/api/semesters?page=1&size=5",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"semesterId\":1,\"semesterName\":\"Spring 2023\",\"startDate\":\"2023-01-10T00:00:00\",\"endDate\":\"2023-05-20T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":2,\"semesterName\":\"Summer 2023\",\"startDate\":\"2023-06-01T00:00:00\",\"endDate\":\"2023-08-31T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":3,\"semesterName\":\"Fall 2023\",\"startDate\":\"2023-09-04T00:00:00\",\"endDate\":\"2023-12-22T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":4,\"semesterName\":\"Spring 2024\",\"startDate\":\"2024-01-08T00:00:00\",\"endDate\":\"2024-05-18T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":5,\"semesterName\":\"Summer 2024\",\"startDate\":\"2024-06-03T00:00:00\",\"endDate\":\"2024-08-30T00:00:00\",\"courseCount\":0,\"courses\":null}],\"pagination\":{\"page\":1,\"pageSize\":5,\"totalItems\":5,\"totalPages\":1}},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
}
]
},
"errors": null,
"traceId": null
}

http://localhost:5049/api/v1/lab-submissions/39bedb86-2c7a-491a-8122-b57f5459cc20/results
{
"status": true,
"message": "Success",
"data": {
"submissionId": "39bedb86-2c7a-491a-8122-b57f5459cc20",
"studentCode": "SE180234",
"submissionStatus": "Done",
"latestJobId": "7578251c-c1fa-47e3-839e-14f8474d6db3",
"jobStatus": "Done",
"totalScore": 10,
"results": [
{
"id": "bdfa7522-e1d3-4e83-85b6-49f5e8c738a9",
"labTestCaseId": "f4ba0c35-e136-4a66-8e79-7a9a33a3c8e7",
"httpMethod": "SOURCE",
"urlTemplate": "project-count:3",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found 3 project(s): PRN232.LMS.Services.csproj, PRN232.LMS.Repositories.csproj, PRN232.LMS.API.csproj",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "73be9e5f-7226-4593-9751-9553fbaa1134",
"labTestCaseId": "829200ea-81fa-4dd5-a3d6-01dd4f6458a8",
"httpMethod": "SOURCE",
"urlTemplate": "file-exists:**/docker-compose.yml",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found: SE180234_NguyenHuuAnh/docker-compose.yml",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "d097de40-f413-4aff-99a2-ae0f1b693aa7",
"labTestCaseId": "e2c47391-d1bd-4467-9293-2054d8694884",
"httpMethod": "SOURCE",
"urlTemplate": "project-name:PRN232.\*.API",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found: PRN232.LMS.API.csproj",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "d5831ddc-f8a7-422f-ac57-ebc1dd234273",
"labTestCaseId": "29c8a1e4-5011-4930-b1e0-f56acc5f924b",
"httpMethod": "SOURCE",
"urlTemplate": "file-exists:**/Dockerfile",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "Found: SE180234_NguyenHuuAnh/PRN232.LMS.API/Dockerfile",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "fa56edd0-fc96-46b2-b7e8-63329e9ebbe0",
"labTestCaseId": "9069fbed-1fb7-4076-9c71-e91322f3e554",
"httpMethod": "GET",
"urlTemplate": "/api/students?search=nguyen&sort=fullName,-dateOfBirth&page=2&size=10&fields=studentId,fullName,email",
"passed": true,
"awardedScore": 1.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[],\"pagination\":{\"page\":2,\"pageSize\":10,\"totalItems\":5,\"totalPages\":1}},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "b9b3868c-5ae4-4ae6-9f32-f31b977bc67f",
"labTestCaseId": "a76a22fd-f0ba-427a-ade3-0eb194edaa95",
"httpMethod": "GET",
"urlTemplate": "/api/students/1",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"studentId\":1,\"fullName\":\"Nguyen Van An\",\"email\":\"student01@fpt.edu.vn\",\"dateOfBirth\":\"2002-02-02T00:00:00\",\"enrollmentCount\":12,\"enrollments\":[{\"enrollmentId\":23,\"studentId\":1,\"courseId\":1,\"enrollDate\":\"2023-01-11T00:00:00\",\"status\":\"Active\",\"student\":null,\"course\":{\"courseId\":1,\"courseName\":\"PRN231 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":{\"semesterId\":1,\"semesterName\":\"Spring 2023\",\"startDate\":\"2023-01-10T00:00:00\",\"endDate\":\"2023-05-20T00:00:00\",\"courseCount\":0,\"courses\":null},\"enrollments\":null}},{\"enrollmentId\":30,\"studentId\":1,\"courseId\":2,\"enrollDate\":\"2023-11-27T00:00:00\",\"status\":\"Pending\",\"student\":null,\"course\":{\"courseId\":2,\"courseName\":\"DBI202 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":{\"semesterId\":1,\"semesterName\":\"Spring 2023\",\"startDate\":\"2023-01-10T00:00:00\",\"endDate\":\"2023-05-20T00:00:00\",\"courseCount\":0,\"courses\":null},\"enrollments\":null}},{\"enrollmentId\":80,\"st",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "a2295431-8d13-44e0-8d42-9b2cebc353cf",
"labTestCaseId": "5fc1d2fc-24cc-4607-bc3d-a391fdec62ab",
"httpMethod": "GET",
"urlTemplate": "/api/enrollments?search=active&sort=-enrollDate&page=1&size=20&fields=enrollmentId,status&expand=student,course",
"passed": true,
"awardedScore": 1.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"enrollmentId\":373,\"status\":\"Active\"},{\"enrollmentId\":294,\"status\":\"Active\"},{\"enrollmentId\":332,\"status\":\"Active\"},{\"enrollmentId\":153,\"status\":\"Active\"},{\"enrollmentId\":339,\"status\":\"Active\"},{\"enrollmentId\":128,\"status\":\"Active\"},{\"enrollmentId\":191,\"status\":\"Active\"},{\"enrollmentId\":77,\"status\":\"Active\"},{\"enrollmentId\":480,\"status\":\"Active\"},{\"enrollmentId\":446,\"status\":\"Active\"},{\"enrollmentId\":250,\"status\":\"Active\"},{\"enrollmentId\":123,\"status\":\"Active\"},{\"enrollmentId\":395,\"status\":\"Active\"},{\"enrollmentId\":305,\"status\":\"Active\"},{\"enrollmentId\":159,\"status\":\"Active\"},{\"enrollmentId\":174,\"status\":\"Active\"},{\"enrollmentId\":138,\"status\":\"Active\"},{\"enrollmentId\":335,\"status\":\"Active\"},{\"enrollmentId\":162,\"status\":\"Active\"},{\"enrollmentId\":253,\"status\":\"Active\"}],\"pagination\":{\"page\":1,\"pageSize\":20,\"totalItems\":149,\"totalPages\":8}},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "ac8245a8-e395-4d14-a485-d343dd11ca89",
"labTestCaseId": "b523c6ba-2361-43f5-b37e-4191b068a205",
"httpMethod": "POST",
"urlTemplate": "/api/enrollments",
"passed": true,
"awardedScore": 1.5,
"actualStatusCode": 201,
"actualResponse": "{\"success\":true,\"message\":\"Resource created successfully\",\"data\":{\"enrollmentId\":501,\"studentId\":1,\"courseId\":1,\"enrollDate\":\"2026-05-27T10:00:00Z\",\"status\":\"Active\",\"student\":null,\"course\":null},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "4aa8f9cd-85ae-43a2-baf2-5d0ac72d3d3e",
"labTestCaseId": "8c286f0e-e414-44c1-a683-3aa9cf24cfeb",
"httpMethod": "GET",
"urlTemplate": "/api/courses?page=1&size=10",
"passed": true,
"awardedScore": 1,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"courseId\":1,\"courseName\":\"PRN231 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":2,\"courseName\":\"DBI202 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":3,\"courseName\":\"MAE101 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":4,\"courseName\":\"SWE201 - Spring 2023\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":5,\"courseName\":\"PRN232 - Summer 2023\",\"semesterId\":2,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":6,\"courseName\":\"SWD392 - Summer 2023\",\"semesterId\":2,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":7,\"courseName\":\"NWC202 - Summer 2023\",\"semesterId\":2,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},{\"courseId\":8,\"courseName\":\"CEA201 - Summer 2023\",\"semesterId\":2,\"enrollmentC",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "92505956-3324-421b-aaf7-52dafe121df7",
"labTestCaseId": "452d7bd1-c507-41ba-809d-130b5434cfcc",
"httpMethod": "POST",
"urlTemplate": "/api/courses",
"passed": true,
"awardedScore": 1,
"actualStatusCode": 201,
"actualResponse": "{\"success\":true,\"message\":\"Resource created successfully\",\"data\":{\"courseId\":21,\"courseName\":\"Advanced REST Architecture\",\"semesterId\":1,\"enrollmentCount\":0,\"semester\":null,\"enrollments\":null},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "a95c1733-540e-4d73-bdeb-7d0d93743c9a",
"labTestCaseId": "2c7d0193-ad1d-496b-ab78-ebc28875b11c",
"httpMethod": "GET",
"urlTemplate": "/api/subjects?page=1&size=10",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"subjectId\":1,\"subjectCode\":\"PRN231\",\"subjectName\":\"Application Development using .NET\",\"credit\":3},{\"subjectId\":2,\"subjectCode\":\"PRN232\",\"subjectName\":\"Advanced Cross-Platform Application\",\"credit\":3},{\"subjectId\":3,\"subjectCode\":\"SWD392\",\"subjectName\":\"Software Architecture and Design\",\"credit\":3},{\"subjectId\":4,\"subjectCode\":\"PRJ301\",\"subjectName\":\"Java Web Application Development\",\"credit\":3},{\"subjectId\":5,\"subjectCode\":\"DBI202\",\"subjectName\":\"Introduction to Database\",\"credit\":3},{\"subjectId\":6,\"subjectCode\":\"MAE101\",\"subjectName\":\"Mathematics for Engineering\",\"credit\":3},{\"subjectId\":7,\"subjectCode\":\"CEA201\",\"subjectName\":\"Computer Organization and Assembly\",\"credit\":3},{\"subjectId\":8,\"subjectCode\":\"NWC202\",\"subjectName\":\"Computer Networking\",\"credit\":3},{\"subjectId\":9,\"subjectCode\":\"SWE201\",\"subjectName\":\"Introduction to Software Engineering\",\"credit\":3},{\"subjectId\":10,\"subjectCode\":\"ITE302\",\"subjectN",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
},
{
"id": "f41b9e11-abe6-4eff-b7bc-de7744109664",
"labTestCaseId": "35987d5a-cdec-4d86-b784-4829f97e6676",
"httpMethod": "GET",
"urlTemplate": "/api/semesters?page=1&size=5",
"passed": true,
"awardedScore": 0.5,
"actualStatusCode": 200,
"actualResponse": "{\"success\":true,\"message\":\"Request processed successfully\",\"data\":{\"items\":[{\"semesterId\":1,\"semesterName\":\"Spring 2023\",\"startDate\":\"2023-01-10T00:00:00\",\"endDate\":\"2023-05-20T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":2,\"semesterName\":\"Summer 2023\",\"startDate\":\"2023-06-01T00:00:00\",\"endDate\":\"2023-08-31T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":3,\"semesterName\":\"Fall 2023\",\"startDate\":\"2023-09-04T00:00:00\",\"endDate\":\"2023-12-22T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":4,\"semesterName\":\"Spring 2024\",\"startDate\":\"2024-01-08T00:00:00\",\"endDate\":\"2024-05-18T00:00:00\",\"courseCount\":0,\"courses\":null},{\"semesterId\":5,\"semesterName\":\"Summer 2024\",\"startDate\":\"2024-06-03T00:00:00\",\"endDate\":\"2024-08-30T00:00:00\",\"courseCount\":0,\"courses\":null}],\"pagination\":{\"page\":1,\"pageSize\":5,\"totalItems\":5,\"totalPages\":1}},\"errors\":null}",
"errorMessage": null,
"manualOverrideScore": null,
"overrideReason": null
}
]
},
"errors": null,
"traceId": null
}
