#!/bin/sh -ex
iptables -F
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

iptables -I INPUT -p udp -s 1.1.1.1 -j ACCEPT
iptables -I OUTPUT -p udp -d  1.1.1.1 -j ACCEPT


iptables -I INPUT -p udp -s 127.0.0.1 -j ACCEPT
iptables -I OUTPUT -p udp -d  127.0.0.1 -j ACCEPT

iptables -I INPUT -p tcp -s 127.0.0.1 -j ACCEPT
iptables -I OUTPUT -p tcp -d  127.0.0.1 -j ACCEPT


iptables -I INPUT -p udp -s 172.19.0.0/16 -j ACCEPT
iptables -I OUTPUT -p udp -d  172.19.0.0/16 -j ACCEPT

iptables -I INPUT -p tcp -s 172.19.0.0/16 -j ACCEPT
iptables -I OUTPUT -p tcp -d  172.19.0.0/16 -j ACCEPT


iptables -A INPUT -p tcp --sport 80 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 80 -j ACCEPT

iptables -A INPUT -p tcp --sport 443 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 443 -j ACCEPT

iptables -A INPUT -p tcp --sport 7000 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 7000 -j ACCEPT

iptables -A INPUT -p tcp --sport 8080 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 8080 -j ACCEPT

iptables -A INPUT -p tcp --sport 8443 -j ACCEPT
iptables -A OUTPUT -p tcp --dport 8443 -j ACCEPT