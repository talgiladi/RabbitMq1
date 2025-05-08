this branch contains:
1. upgraded rabbitmq (.net version 7.1.2)
2. retries queue with delay
3. runs on terminal as two different apps - producer + consumer. does not run on docker
4. assumes we have rabbitmq on localhost (can be on docker using: docker run -d --hostname rabbitmq-host --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management)
